using UiTest;

namespace UiXaml;

public partial class MainPage : ContentPage
{
    bool _shown;

    public MainPage()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(Chips, Stations.AllGenres);
        List.ItemsSource = Stations.All;
    }

    // The first row attached to the window: the list is on screen with the next frame.
    void OnRowLoaded(object? sender, EventArgs e)
    {
        if (_shown)
            return;
        _shown = true;
        Marks.Write("content");
    }
}
