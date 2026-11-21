namespace EngineTest;

sealed class MainPage : ContentPage
{
    static readonly string[] BandNames = ["31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k"];

    readonly Picker _station = new() { Title = "Station" };
    readonly Entry _custom = new() { Placeholder = "or a custom stream URL" };
    readonly Entry _relayHost = new() { Placeholder = "relay PC address, e.g. 192.168.1.10" };
    readonly Entry _netBuffer = new() { Keyboard = Keyboard.Numeric };
    readonly Entry _preBuffer = new() { Keyboard = Keyboard.Numeric };
    readonly Label _state = new() { FontAttributes = FontAttributes.Bold };
    readonly Label _title = new();
    readonly Label _format = new() { FontSize = 12 };
    readonly Label _rate = new() { FontSize = 12 };
    readonly Label _log = new() { FontSize = 10, FontFamily = "monospace" };
    readonly GraphicsView _spectrum = new() { Drawable = new Spectrum(), HeightRequest = 100 };
    int _logVersion = -1;

    public MainPage()
    {
        foreach (var name in Stations.Names)
            _station.Items.Add(name);
        _station.SelectedIndex = Preferences.Get("station", 0);
        _relayHost.Text = Preferences.Get("relay", "");
        _netBuffer.Text = Player.Instance.NetBuffer.ToString();
        _preBuffer.Text = Player.Instance.PreBuffer.ToString();

        var play = new Button { Text = "Play" };
        play.Clicked += (_, _) => Play();
        var stop = new Button { Text = "Stop" };
        stop.Clicked += (_, _) => Player.Instance.Stop("user");
        var record = new Switch { IsToggled = Player.Instance.Recording };
        record.Toggled += (_, e) => Player.Instance.Recording = e.Value;
        var javaConnect = new Switch();
        javaConnect.Toggled += (_, e) => Player.Instance.JavaConnect = e.Value;
        var gcInConnect = new Switch();
        gcInConnect.Toggled += (_, e) => Player.Instance.GcInConnect = e.Value;
        var share = new Button { Text = "Share log" };
        share.Clicked += async (_, _) => await ShareLog();
        var http = new Button { Text = "Test the app's own plain http" };
        http.Clicked += async (_, _) => await ProbeHttp();
        var push = new Button { Text = "Push check" };
        push.Clicked += async (_, _) => await PushCheck.Run(Player.Instance.GcInConnect);

        var equalizer = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 8 };
        for (var band = 0; band < BandNames.Length; band++)
        {
            var index = band;
            var slider = new Slider(-12, 12, 0);
            slider.ValueChanged += (_, e) => Player.Instance.SetGain(index, e.NewValue);
            equalizer.RowDefinitions.Add(new(GridLength.Auto));
            equalizer.Add(new Label { Text = $"{BandNames[band]} Hz", FontSize = 12, VerticalOptions = LayoutOptions.Center }, 0, band);
            equalizer.Add(slider, 1, band);
        }

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 12,
                Spacing = 8,
                Children =
                {
                    _station,
                    _custom,
                    _relayHost,
                    Row(new Label { Text = "Buffer, ms", VerticalOptions = LayoutOptions.Center }, _netBuffer, new Label { Text = "Pre-buffer, %", VerticalOptions = LayoutOptions.Center }, _preBuffer),
                    Row(play, stop, new Label { Text = "Record", VerticalOptions = LayoutOptions.Center }, record),
#if ANDROID
                    Row(new Label { Text = "Java connect", VerticalOptions = LayoutOptions.Center }, javaConnect, new Label { Text = "GC in connect", VerticalOptions = LayoutOptions.Center }, gcInConnect),
#endif
                    _state,
                    _title,
                    _format,
                    _spectrum,
                    _rate,
                    equalizer,
                    Row(share, http),
                    push,
                    _log,
                },
            },
        };

        Player.Instance.Changed += () => MainThread.BeginInvokeOnMainThread(Refresh);
        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(15), () =>
        {
            _spectrum.Invalidate();
            return true;
        });
        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(500), () =>
        {
            _rate.Text = $"spectrum: {Player.SpectrumRate}";
            if (Log.Version != _logVersion)
            {
                _logVersion = Log.Version;
                _log.Text = Log.Tail();
            }
            return true;
        });
        Refresh();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        Log.Write("start", $"notification permission: {status}");
    }

    void Play()
    {
        var custom = _custom.Text?.Trim();
        var relayHost = _relayHost.Text?.Trim() ?? "";
        var (name, url) = string.IsNullOrEmpty(custom) ? Stations.At(_station.SelectedIndex, relayHost) : ("Custom URL", custom);
        Preferences.Set("station", _station.SelectedIndex);
        Preferences.Set("relay", relayHost);
        if (int.TryParse(_netBuffer.Text, out var netBuffer))
            Player.Instance.NetBuffer = netBuffer;
        if (int.TryParse(_preBuffer.Text, out var preBuffer))
            Player.Instance.PreBuffer = preBuffer;
        Player.Instance.Play(name, url);
    }

    void Refresh()
    {
        _state.Text = Player.Instance.State;
        _title.Text = Player.Instance.Title ?? "(no title)";
        _format.Text = Player.Instance.Format ?? "";
    }

    static async Task ShareLog()
    {
        var file = new ShareFile(Log.FilePath);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = "Engine test log", File = file });
    }

    // The app's own requests go through the platform's HTTP stack, which Android's cleartext
    // policy covers. BASS's connections don't; this shows the policy is really in force.
    static async Task ProbeHttp()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            using var response = await client.GetAsync("http://ice1.somafm.com/", HttpCompletionOption.ResponseHeadersRead);
            Log.Write("http", $"app's own plain http request: {(int)response.StatusCode}");
        }
        catch (Exception e)
        {
            Log.Write("http", $"app's own plain http request failed: {e.GetType().Name}: {e.Message}");
        }
    }

    static HorizontalStackLayout Row(params View[] views)
    {
        var row = new HorizontalStackLayout { Spacing = 8 };
        foreach (var view in views)
            row.Add(view);
        return row;
    }
}
