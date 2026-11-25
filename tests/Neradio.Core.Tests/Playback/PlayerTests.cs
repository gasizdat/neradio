using System.Net;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using Neradio.Core.Playback;
using Neradio.Core.Stations;

namespace Neradio.Core.Tests.Playback;

public sealed class PlayerTests
{
    private static readonly Uri Primary = new("https://a.example/live");
    private static readonly Uri Alternate = new("https://b.example/live");
    private static readonly Uri Other = new("https://c.example/live");
    private static readonly Uri Pls = new("https://lists.example/station.pls");
    private static readonly Uri M3u = new("https://lists.example/station.m3u");
    private static readonly Uri Hls = new("https://lists.example/station.m3u8");

    private readonly FakeTimeProvider _time = new();
    private readonly SettableClock _clock;
    private readonly FakeEngine _engine = new();
    private readonly FakeHttp _http;
    private readonly Player _player;
    private readonly DateTimeOffset _start;
    private readonly List<string> _titleChanges = [];
    private readonly List<PlayerState> _stateChanges = [];

    public PlayerTests()
    {
        _clock = new SettableClock(_time);
        _http = new FakeHttp(_time);
        var client = new HttpClient(_http);
        _player = new Player(client, _clock, _engine);
        _player.TitleChanged += _titleChanges.Add;
        _player.StateChanged += () => _stateChanges.Add(_player.State);
        _start = _time.GetUtcNow();
    }

    // One station at a time

    [Fact]
    public void PlayingAnotherStation()
    {
        _http.Stream(Primary);
        _http.Stream(Other);
        _player.Play(Station(Primary));
        Plays(Primary);

        var second = Station(Other);
        _player.Play(second);
        Plays(Other);

        var first = _engine.Streams[0];
        Assert.True(first.IsStopped);
        _http.LastBody(Primary).Send([9]);
        Assert.DoesNotContain((byte)9, first.Written);
        Assert.False(_engine.Streams[1].IsStopped);
        Assert.Same(second, _player.Station);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    // Stopping a live stream

    [Fact]
    public void StopAndPlayAgain()
    {
        _http.Stream(Primary);
        var station = Station(Primary);
        _player.Play(station);
        Plays(Primary);

        _player.Stop(StopKind.User);
        Advance(60);
        _player.Play(station);
        Plays(Primary);

        Assert.True(_engine.Streams[0].IsStopped);
        Assert.Equal(2, _engine.Streams.Count);
        Assert.Equal([0, 60], RequestTimes());
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void StoppedStationStaysSelected()
    {
        _http.Stream(Primary);
        var station = Station(Primary);
        _player.Play(station);
        Plays(Primary);

        _player.Stop(StopKind.User);

        Assert.Equal(PlayerState.Stopped, _player.State);
        Assert.Same(station, _player.Station);
        Assert.True(_engine.Streams[0].IsStopped);
    }

    [Fact]
    public void StoppingWhileReconnecting()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _http.Refuse(Primary);
        _http.LastBody(Primary).End();
        Assert.Equal(PlayerState.Reconnecting, _player.State);

        _player.Stop(StopKind.User);
        Advance(600);

        Assert.Single(_http.Requests);
    }

    // Lost connections

    [Fact]
    public void ServerClosesTheConnection()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        Listen(Primary, 60);

        _http.LastBody(Primary).End();

        Assert.Equal([0, 60], RequestTimes());
    }

    [Fact]
    public void StalledStream()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        Listen(Primary, 60);

        Advance(9.9);
        Assert.Single(_http.Requests);
        Advance(0.1);

        Assert.Equal([0, 70], RequestTimes());
    }

    [Fact]
    public void ShortPauseInTheData()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);

        Advance(9);
        _http.LastBody(Primary).Send([2]);
        Advance(9);

        Assert.Single(_http.Requests);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void AttemptWithoutAudioDataFor10Seconds()
    {
        _http.Stream(Primary);
        _http.Stream(Alternate);
        _player.Play(Station(Primary, Alternate));

        Advance(9.9);
        Assert.Single(_http.Requests);
        Advance(0.1);

        Assert.Equal([Primary, Alternate], _http.Requested);
        Assert.Equal([0, 10], RequestTimes());
        Assert.True(_engine.Streams[0].IsStopped);
    }

    // Connection rounds

    [Fact]
    public void DeadPrimaryWhenStarting()
    {
        _http.Stream(Alternate);

        _player.Play(Station(Primary, Alternate));
        Plays(Alternate);

        Assert.Equal([Primary, Alternate], _http.Requested);
        Assert.Equal([0, 0], RequestTimes());
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void LostConnectionOnAnAlternate()
    {
        _http.Stream(Alternate);
        _player.Play(Station(Primary, Alternate));
        Plays(Alternate);
        Listen(Alternate, 60);
        _http.Refuse(Alternate);

        _http.LastBody(Alternate).End();

        Assert.Equal([Primary, Alternate, Alternate, Primary], _http.Requested);
    }

    [Fact]
    public void ReconnectingIsShown()
    {
        _http.Stream(Primary);
        var station = Station(Primary);
        _player.Play(station);
        Plays(Primary);

        _http.LastBody(Primary).End();

        Assert.Equal(PlayerState.Reconnecting, _player.State);
        Assert.Same(station, _player.Station);
    }

    // Network changes

    [Fact]
    public void PlayingConnectionIsDropped()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        Advance(5);
        var old = _http.LastBody(Primary);

        _player.NetworkChanged();
        old.Send([9]);

        Assert.Equal([0, 5], RequestTimes());
        Assert.DoesNotContain((byte)9, _engine.Streams[0].Written);
        Assert.False(_engine.Streams[0].IsStopped);
        Assert.Equal(PlayerState.Reconnecting, _player.State);
    }

    [Fact]
    public void RoundUnderWayIsAbandoned()
    {
        _http.Hang(Primary);
        _player.Play(Station(Primary, Alternate));
        Advance(5);

        _player.NetworkChanged();
        Advance(5);
        Assert.Equal([Primary, Primary], _http.Requested);
        Advance(5);

        Assert.Equal([Primary, Primary, Alternate], _http.Requested);
        Assert.Equal([0, 5, 15], RequestTimes());
    }

    [Fact]
    public void WaitInProgressIsSkipped()
    {
        _player.Play(Station(Primary));
        Advance(1);
        Advance(0.5);

        _player.NetworkChanged();

        Assert.Equal([0, 1, 1.5], RequestTimes());
    }

    [Fact]
    public void FirstRoundOnTheNewNetworkFails()
    {
        _player.Play(Station(Primary));
        foreach (var wait in new[] { 1, 2, 4, 8, 16, 30 })
            Advance(wait);
        Advance(10);

        _player.NetworkChanged();
        Advance(0.9);
        Assert.Equal(8, _http.Requests.Count);
        Advance(0.1);

        double[] rounds = [0, 1, 3, 7, 15, 31, 61, 71, 72];
        Assert.Equal(rounds, RequestTimes());
    }

    [Fact]
    public void NetworkChangeWhileAnAlternatePlays()
    {
        _http.Stream(Alternate);
        _player.Play(Station(Primary, Alternate));
        Plays(Alternate);

        _player.NetworkChanged();

        Assert.Equal([Primary, Alternate, Alternate], _http.Requested);
    }

    [Fact]
    public void NetworkChangeAfterAStop()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.Stop(StopKind.User);

        _player.NetworkChanged();

        Assert.Single(_http.Requests);
        Assert.Equal(PlayerState.Stopped, _player.State);
    }

    // Buffered audio while reconnecting

    [Fact]
    public void BufferedAudioPlaysUntilTheNewConnectionPlays()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        Listen(Primary, 60);

        _http.LastBody(Primary).End();
        Assert.False(_engine.Streams[0].IsStopped);
        Plays(Primary);

        Assert.True(_engine.Streams[0].IsStopped);
        Assert.False(_engine.Streams[1].IsStopped);
    }

    [Fact]
    public void FailedAttemptLeavesTheBufferedAudioPlaying()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        Listen(Primary, 60);

        _http.LastBody(Primary).End();
        _http.LastBody(Primary).End();

        Assert.False(_engine.Streams[0].IsStopped);
        Assert.True(_engine.Streams[1].IsStopped);
    }

    // Waits between rounds

    [Fact]
    public void EveryStreamKeepsFailing()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary, Alternate));
        Plays(Primary);
        Listen(Primary, 60);
        _http.Refuse(Primary);

        _http.LastBody(Primary).End();
        foreach (var wait in new[] { 1, 2, 4, 8, 16, 30, 30 })
            Advance(wait);

        double[] rounds = [60, 61, 63, 67, 75, 91, 121, 151];
        Assert.Equal([0, .. rounds], RequestTimes(Primary));
        Assert.Equal(rounds, RequestTimes(Alternate));
    }

    [Fact]
    public void LongOutage()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        Listen(Primary, 60);
        _http.Refuse(Primary);
        _http.LastBody(Primary).End();

        for (var second = 0; second < 3600; second++)
            Advance(1);

        var times = RequestTimes();
        Assert.Equal(30, times[^1] - times[^2]);
        Assert.True(60 + 3600 - times[^1] < 30);
    }

    [Fact]
    public void StreamDropsSoonAfterReconnecting()
    {
        _player.Play(Station(Primary));
        _http.Stream(Primary);
        Advance(1);
        Plays(Primary);
        Listen(Primary, 20);

        _http.LastBody(Primary).End();
        Advance(1.9);
        Assert.Equal([0, 1], RequestTimes());
        Advance(0.1);

        Assert.Equal([0, 1, 23], RequestTimes());
    }

    [Fact]
    public void StreamDropsAfterPlayingAWhile()
    {
        _player.Play(Station(Primary));
        _http.Stream(Primary);
        Advance(1);
        Plays(Primary);
        Listen(Primary, 60);

        _http.LastBody(Primary).End();

        Assert.Equal([0, 1, 61], RequestTimes());
    }

    [Fact]
    public void ClockSetForwardWhilePlaying()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _clock.Shift = TimeSpan.FromHours(1);
        Listen(Primary, 10);

        _http.LastBody(Primary).End();
        Assert.Single(_http.Requests);
        Advance(1);

        Assert.Equal([0, 11], RequestTimes());
    }

    // Playlist URLs

    [Fact]
    public void PlsPlaylist()
    {
        _http.Text(Pls, "[playlist]\nNumberOfEntries=2\nFile2=https://b.example/live\nFile1=https://a.example/live\n", "audio/x-scpls");
        _http.Stream(Alternate);

        _player.Play(Station(Pls));
        Plays(Alternate);

        Assert.Equal([Pls, Primary, Alternate], _http.Requested);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void M3uPlaylist()
    {
        _http.Text(M3u, "#EXTM3U\n#EXTINF:-1,Station\nlive\n");
        var listed = new Uri("https://lists.example/live");
        _http.Stream(listed);

        _player.Play(Station(M3u));
        Plays(listed);

        Assert.Equal([M3u, listed], _http.Requested);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void PlaylistWithAByteOrderMark()
    {
        _http.Text(M3u, "﻿#EXTM3U\nhttps://a.example/live\n");
        _http.Stream(Primary);

        _player.Play(Station(M3u));
        Plays(Primary);

        Assert.Equal([M3u, Primary], _http.Requested);
    }

    [Fact]
    public void HlsPlaylistIsNotAListOfStreams()
    {
        var segment = new Uri("https://lists.example/100.ts");
        _http.Text(Hls, "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXT-X-MEDIA-SEQUENCE:100\n#EXTINF:10,\n100.ts\n", "application/vnd.apple.mpegurl");
        _http.Bytes(segment, MpegTsWriter.Segment([1, 2, 3]));

        _player.Play(Station(Hls));
        _engine.Streams[0].Play();

        Assert.Equal([Hls, segment], _http.Requested);
        Assert.Equal("audio/aac", _engine.Streams[0].ContentType);
        Assert.Equal([1, 2, 3], _engine.Streams[0].Written);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void HlsPlaylistListedInAPlaylist()
    {
        var listed = new Uri("https://lists.example/hls/live.m3u8");
        var segment = new Uri("https://lists.example/hls/100.ts");
        _http.Text(M3u, "#EXTM3U\nhttps://lists.example/hls/live.m3u8\n");
        _http.Text(listed, "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXT-X-MEDIA-SEQUENCE:100\n#EXTINF:10,\n100.ts\n");
        _http.Bytes(segment, MpegTsWriter.Segment([1, 2, 3]));

        _player.Play(Station(M3u));
        _engine.Streams[0].Play();

        Assert.Equal([M3u, listed, segment], _http.Requested);
        Assert.Equal("audio/aac", _engine.Streams[0].ContentType);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void HlsReconnectGoesOnAfterTheLastSegment()
    {
        _http.Text(Hls, "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXT-X-MEDIA-SEQUENCE:100\n#EXTINF:10,\n100.ts\n");
        _http.Bytes(new Uri("https://lists.example/100.ts"), MpegTsWriter.Segment([100]));
        _player.Play(Station(Hls));
        _engine.Streams[0].Play();

        _player.NetworkChanged();

        _http.Text(Hls, "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXT-X-MEDIA-SEQUENCE:100\n#EXTINF:10,\n100.ts\n#EXTINF:10,\n101.ts\n");
        _http.Bytes(new Uri("https://lists.example/101.ts"), MpegTsWriter.Segment([101]));
        Advance(5);

        Assert.Equal([101], _engine.Streams[1].Written);
        Assert.Single(_http.Requested, url => url.AbsolutePath == "/100.ts");
    }

    [Fact]
    public void HlsConnectionDroppedBeforeItPlays()
    {
        _http.Text(Hls, "#EXTM3U\n#EXT-X-TARGETDURATION:10\n#EXT-X-MEDIA-SEQUENCE:100\n#EXTINF:10,\n100.ts\n");
        _http.Bytes(new Uri("https://lists.example/100.ts"), MpegTsWriter.Segment([100]));
        _player.Play(Station(Hls));

        _player.NetworkChanged();

        Assert.True(_engine.Streams[0].IsStopped);
        Assert.Equal(2, _engine.Streams.Count);
        Assert.Equal([100], _engine.Streams[1].Written);
    }

    [Fact]
    public void PlaylistCannotBeDownloaded()
    {
        _player.Play(Station(Pls));
        Advance(1);

        Assert.Equal([0, 1], RequestTimes(Pls));
        Assert.Equal(PlayerState.Reconnecting, _player.State);
    }

    [Fact]
    public void PlaylistServerDoesNotAnswer()
    {
        _http.Hang(Pls);
        _http.Stream(Alternate);
        _player.Play(Station(Pls, Alternate));

        Advance(9.9);
        Assert.Equal([Pls], _http.Requested);
        Advance(0.1);
        Plays(Alternate);

        Assert.Equal([0, 10], RequestTimes());
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void PlaylistWithoutUsableStreams()
    {
        _http.Text(Pls, "[playlist]\nFile1=ftp://a.example/live\nTitle1=Station\n", "audio/x-scpls");

        _player.Play(Station(Pls));
        Advance(1);

        Assert.Equal([0, 1], RequestTimes(Pls));
        Assert.Equal(PlayerState.Reconnecting, _player.State);
    }

    [Fact]
    public void LostConnectionOnAStreamFromAPlaylist()
    {
        _http.Text(Pls, "[playlist]\nFile1=https://a.example/live\nFile2=https://b.example/live\n", "audio/x-scpls");
        _http.Stream(Alternate);
        _player.Play(Station(Pls));
        Plays(Alternate);
        Listen(Alternate, 60);

        _http.LastBody(Alternate).End();
        Assert.Equal([Pls, Primary, Alternate, Alternate], _http.Requested);
        _http.LastBody(Alternate).End();

        Assert.Equal([Pls, Primary, Alternate, Alternate, Pls, Primary], _http.Requested);
    }

    [Fact]
    public void PlaylistOfUnsupportedStreams()
    {
        _http.Text(Pls, "[playlist]\nFile1=https://a.example/live\n", "audio/x-scpls");
        _http.Stream(Primary, "audio/flac");
        _player.Play(Station(Pls));

        _engine.Streams[0].RejectFormat();
        Advance(600);

        Assert.Equal(PlayerState.Unsupported, _player.State);
        Assert.Equal([Pls, Primary], _http.Requested);
    }

    // Unsupported formats

    [Fact]
    public void PrimaryInAnUnsupportedFormat()
    {
        _http.Stream(Primary, "audio/flac");
        _http.Stream(Alternate);
        _player.Play(Station(Primary, Alternate));

        _engine.Streams[0].RejectFormat();
        Plays(Alternate);

        Assert.True(_engine.Streams[0].IsStopped);
        Assert.Equal([Primary, Alternate], _http.Requested);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void UnsupportedStreamIsSkippedUntilPlaybackStops()
    {
        _http.Stream(Primary, "audio/flac");
        _http.Stream(Alternate);
        var station = Station(Primary, Alternate);
        _player.Play(station);
        _engine.Streams[0].RejectFormat();
        Plays(Alternate);
        Listen(Alternate, 60);
        _http.Refuse(Alternate);

        _http.LastBody(Alternate).End();
        Advance(1);
        Assert.Equal([Primary, Alternate, Alternate, Alternate], _http.Requested);

        _player.Stop(StopKind.User);
        _player.Play(station);
        Assert.Equal(Primary, _http.Requested.Last());
    }

    [Fact]
    public void FormatRejectedAfterTheConnectionIsLost()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _http.LastBody(Primary).End();

        var error = Record.Exception(_engine.Streams[0].RejectFormat);
        Advance(1);

        Assert.Null(error);
        Assert.Equal([0, 1], RequestTimes());
    }

    [Fact]
    public void NoPlayableStream()
    {
        _http.Stream(Primary, "audio/flac");
        _http.Stream(Alternate, "audio/flac");
        _player.Play(Station(Primary, Alternate));

        _engine.Streams[0].RejectFormat();
        _engine.Streams[1].RejectFormat();
        Advance(600);

        Assert.Equal(PlayerState.Unsupported, _player.State);
        Assert.Equal([Primary, Alternate], _http.Requested);
    }

    [Fact]
    public void ServerAnswersWithAnError()
    {
        _http.Status(Primary, HttpStatusCode.NotFound);
        _player.Play(Station(Primary));
        Advance(1);
        Advance(2);

        _http.Stream(Primary);
        Advance(4);
        Plays(Primary);

        Assert.Equal([0, 1, 3, 7], RequestTimes());
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void WiFiLoginPage()
    {
        _http.Text(Primary, "<html><body>Log in to the Wi-Fi</body></html>", "text/html");
        _player.Play(Station(Primary));
        Advance(1);
        Advance(2);

        _http.Stream(Primary);
        Advance(4);
        Plays(Primary);

        Assert.Equal([0, 1, 3, 7], RequestTimes());
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    // Phone calls and other interruptions

    [Fact]
    public void IncomingCall()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);

        _player.Stop(StopKind.Interruption);
        Assert.Equal(PlayerState.Interrupted, _player.State);
        Assert.True(_engine.Streams[0].IsStopped);
        Advance(120);
        Assert.Single(_http.Requests);
        _player.EndInterruption();
        Plays(Primary);

        Assert.Equal([0, 120], RequestTimes());
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void AfterAnInterruptionThePrimaryComesFirst()
    {
        _http.Stream(Alternate);
        _player.Play(Station(Primary, Alternate));
        Plays(Alternate);
        _player.Stop(StopKind.Interruption);

        _player.EndInterruption();

        Assert.Equal([Primary, Alternate, Primary, Alternate], _http.Requested);
    }

    [Fact]
    public void AfterAnInterruptionTheWaitsStartOver()
    {
        _player.Play(Station(Primary));
        foreach (var wait in new[] { 1, 2, 4, 8, 16, 30 })
            Advance(wait);
        _player.Stop(StopKind.Interruption);

        _player.EndInterruption();
        Advance(1);

        Assert.Equal([0, 1, 3, 7, 15, 31, 61, 61, 62], RequestTimes());
    }

    [Fact]
    public void SameTitleAfterAnInterruption()
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();
        _player.Stop(StopKind.Interruption);

        _player.EndInterruption();
        SendIcy("Artist A - Song 1");
        _engine.Streams[^1].Play();
        Decode();

        Assert.Equal("Artist A - Song 1", _player.Title);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void InterruptionWhileConnecting()
    {
        _http.Hang(Primary);
        _player.Play(Station(Primary));

        _player.Stop(StopKind.Interruption);
        _http.Stream(Primary);
        _player.EndInterruption();
        Plays(Primary);

        Assert.Equal(2, _http.Requests.Count);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void StoppedDuringACall()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.Stop(StopKind.Interruption);

        _player.Stop(StopKind.User);
        _player.EndInterruption();

        Assert.Single(_http.Requests);
        Assert.Equal(PlayerState.Stopped, _player.State);
    }

    [Fact]
    public void HeadphonesUnpluggedDuringACall()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.Stop(StopKind.Interruption);

        _player.Stop(StopKind.System);
        _player.EndInterruption();

        Assert.Single(_http.Requests);
        Assert.Equal(PlayerState.Stopped, _player.State);
    }

    [Fact]
    public void InterruptionAfterAStop()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.Stop(StopKind.User);

        _player.Stop(StopKind.Interruption);
        _player.EndInterruption();

        Assert.Single(_http.Requests);
        Assert.Equal(PlayerState.Stopped, _player.State);
    }

    [Fact]
    public void SecondInterruptionDuringTheFirst()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.Stop(StopKind.Interruption);

        _player.Stop(StopKind.Interruption);
        _player.EndInterruption();

        Assert.Equal(2, _http.Requests.Count);
    }

    [Fact]
    public void NetworkChangeDuringAnInterruption()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.Stop(StopKind.Interruption);

        _player.NetworkChanged();

        Assert.Single(_http.Requests);
        Assert.Equal(PlayerState.Interrupted, _player.State);
    }

    // Track titles

    [Fact]
    public void NewTitle()
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();

        SendIcy("Artist B - Song 2");
        Decode();

        Assert.Equal("Artist B - Song 2", _player.Title);
        Assert.Equal(["Artist A - Song 1", "Artist B - Song 2"], _titleChanges);
    }

    [Fact]
    public void TitleInAnOggStream()
    {
        _http.Stream(Primary, "audio/ogg");
        _player.Play(Station(Primary));
        Plays(Primary);

        _engine.Streams[0].ChangeTitle("Artist A - Song 1");

        Assert.Equal("Artist A - Song 1", _player.Title);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void OggTitleBeforeItsAudioPlays()
    {
        _http.Stream(Primary, "audio/ogg");
        _player.Play(Station(Primary));
        _http.LastBody(Primary).Send([1]);

        _engine.Streams[0].ChangeTitle("Artist A - Song 1");
        Assert.Null(_player.Title);
        _engine.Streams[0].Play();

        Assert.Equal("Artist A - Song 1", _player.Title);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTitle(string empty)
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();

        SendIcy(empty);
        Decode();

        Assert.Null(_player.Title);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void StationWithoutTitles()
    {
        PlayIcy();

        SendIcy();
        SendIcy();
        Decode();

        Assert.Null(_player.Title);
        Assert.Empty(_titleChanges);
    }

    [Fact]
    public void TitleIsTrimmed()
    {
        PlayIcy();

        SendIcy(" Artist A - Song 1 ");
        Decode();

        Assert.Equal("Artist A - Song 1", _player.Title);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    // Repeated titles aren't changes

    [Fact]
    public void StreamRepeatsItsTitle()
    {
        PlayIcy();

        SendIcy("Artist A - Song 1");
        SendIcy("Artist A - Song 1");
        SendIcy("Artist A - Song 1");
        Decode();

        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void SameTitleAfterAnEmptyOne()
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();
        SendIcy("");
        Decode();

        SendIcy("Artist A - Song 1");
        Decode();

        Assert.Equal("Artist A - Song 1", _player.Title);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void SameTitleAfterAReconnect()
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();
        _http.LastBody(Primary).End();

        Advance(1);
        SendIcy("Artist A - Song 1");
        _engine.Streams[^1].Play();
        Decode();

        Assert.Equal(2, _engine.Streams.Count);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void SameTitleAfterTheUserPlaysTheStationAgain()
    {
        var station = PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();
        _player.Stop(StopKind.User);

        _player.Play(station);
        SendIcy();
        _engine.Streams[^1].Play();
        SendIcy("Artist A - Song 1");
        Decode();

        Assert.Equal(["Artist A - Song 1", "Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void StopClearsTheTitle()
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();

        _player.Stop(StopKind.User);

        Assert.Null(_player.Title);
    }

    // Titles show with their audio

    [Fact]
    public void TitleArrivesAheadOfItsAudio()
    {
        PlayIcy();
        var stream = _engine.Streams[0];
        SendIcy("Artist A - Song 1");
        stream.Decoded = 4;

        for (var second = 0; second < 30; second += 5)
        {
            Advance(5);
            SendIcy();
        }
        Assert.Null(_player.Title);
        Assert.Empty(_titleChanges);
        stream.Decoded = 8;
        Advance(0.25);

        Assert.Equal("Artist A - Song 1", _player.Title);
        Assert.Equal(["Artist A - Song 1"], _titleChanges);
    }

    [Fact]
    public void NewConnectionsTitleWaitsForItsAudio()
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        Decode();
        _http.LastBody(Primary).End();
        Advance(1);

        SendIcy("Artist B - Song 2");
        Advance(0.25);
        Assert.Equal("Artist A - Song 1", _player.Title);
        _engine.Streams[^1].Play();
        Decode();

        Assert.Equal("Artist B - Song 2", _player.Title);
        Assert.Equal(["Artist A - Song 1", "Artist B - Song 2"], _titleChanges);
    }

    [Fact]
    public void LostConnectionsLastTitleShowsBeforeTheNewConnectionPlays()
    {
        PlayIcy();
        SendIcy("Artist A - Song 1");
        _http.LastBody(Primary).End();
        Advance(1);
        SendIcy("Artist B - Song 2");

        _engine.Streams[^1].Play();
        Decode();

        Assert.Equal(["Artist A - Song 1", "Artist B - Song 2"], _titleChanges);
    }

    // Setting the sleep timer

    [Fact]
    public void SleepTimerRunsOut()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);

        _player.SetSleepTimer(TimeSpan.FromMinutes(30));
        Listen(Primary, 30 * 60 - 1);
        Assert.Equal(PlayerState.Playing, _player.State);
        Advance(1);

        Assert.Equal(PlayerState.Stopped, _player.State);
        Assert.True(_engine.Streams[0].IsStopped);
        Assert.Null(_player.SleepTimeLeft);
    }

    [Fact]
    public void TimeLeftIsShown()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);

        _player.SetSleepTimer(TimeSpan.FromMinutes(30));
        Listen(Primary, 10 * 60);

        Assert.Equal(TimeSpan.FromMinutes(20), _player.SleepTimeLeft);
    }

    [Fact]
    public void ClockSetBackWhileTheSleepTimerRuns()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));

        _clock.Shift = TimeSpan.FromHours(-1);
        Listen(Primary, 10 * 60);
        Assert.Equal(TimeSpan.FromMinutes(20), _player.SleepTimeLeft);
        Listen(Primary, 20 * 60);

        Assert.Equal(PlayerState.Stopped, _player.State);
        Assert.True(_engine.Streams[0].IsStopped);
    }

    [Fact]
    public void SleepTimerRunsOutWhileReconnecting()
    {
        _player.Play(Station(Primary));
        _player.SetSleepTimer(TimeSpan.FromMinutes(1));

        Advance(60);
        var attempts = _http.Requests.Count;
        Advance(600);

        Assert.Equal(PlayerState.Stopped, _player.State);
        Assert.Equal(attempts, _http.Requests.Count);
    }

    [Fact]
    public void SleepTimerRunsOutDuringAnInterruption()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(1));
        _player.Stop(StopKind.Interruption);

        Advance(60);
        _player.EndInterruption();

        Assert.Equal(PlayerState.Stopped, _player.State);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public void SleepTimerSetAgain()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));
        Listen(Primary, 10 * 60);

        _player.SetSleepTimer(TimeSpan.FromMinutes(5));
        Listen(Primary, 5 * 60);

        Assert.Equal(PlayerState.Stopped, _player.State);
    }

    // Cancelling the sleep timer

    [Fact]
    public void UserCancelsTheSleepTimer()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));

        _player.CancelSleepTimer();
        Listen(Primary, 31 * 60);

        Assert.Equal(PlayerState.Playing, _player.State);
        Assert.Null(_player.SleepTimeLeft);
    }

    [Fact]
    public void UserStopsPlaybackWhileTheSleepTimerRuns()
    {
        _http.Stream(Primary);
        var station = Station(Primary);
        _player.Play(station);
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));

        _player.Stop(StopKind.User);
        _player.Play(station);
        Plays(Primary);
        Listen(Primary, 31 * 60);

        Assert.Null(_player.SleepTimeLeft);
        Assert.Equal(PlayerState.Playing, _player.State);
    }

    [Fact]
    public void AnotherStationWhileTheSleepTimerRuns()
    {
        _http.Stream(Primary);
        _http.Stream(Other);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));
        Listen(Primary, 10 * 60);

        _player.Play(Station(Other));
        Plays(Other);
        Assert.Equal(TimeSpan.FromMinutes(20), _player.SleepTimeLeft);
        Listen(Other, 20 * 60);

        Assert.Equal(PlayerState.Stopped, _player.State);
    }

    [Fact]
    public void PhoneCallWhileTheSleepTimerRuns()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));

        _player.Stop(StopKind.Interruption);
        Advance(5 * 60);
        _player.EndInterruption();
        Plays(Primary);

        Assert.Equal(PlayerState.Playing, _player.State);
        Assert.Equal(TimeSpan.FromMinutes(25), _player.SleepTimeLeft);
    }

    [Fact]
    public void AnotherAppTakesOverWhileTheSleepTimerRuns()
    {
        _http.Stream(Primary);
        var station = Station(Primary);
        _player.Play(station);
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));

        _player.Stop(StopKind.System);
        Advance(5 * 60);
        _player.Play(station);
        Plays(Primary);
        Assert.Equal(TimeSpan.FromMinutes(25), _player.SleepTimeLeft);
        Listen(Primary, 25 * 60);

        Assert.Equal(PlayerState.Stopped, _player.State);
    }

    [Fact]
    public void UnsupportedStationKeepsTheSleepTimer()
    {
        _http.Stream(Primary, "audio/flac");
        _player.Play(Station(Primary));
        _player.SetSleepTimer(TimeSpan.FromMinutes(30));

        _engine.Streams[0].RejectFormat();

        Assert.Equal(PlayerState.Unsupported, _player.State);
        Assert.Equal(TimeSpan.FromMinutes(30), _player.SleepTimeLeft);
    }

    // State changes, for the platform's media controls

    [Fact]
    public void StateChangesAsAStationPlaysAndReconnects()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _http.LastBody(Primary).End();
        Advance(1);
        Plays(Primary);
        _player.Stop(StopKind.User);

        PlayerState[] states = [PlayerState.Connecting, PlayerState.Playing, PlayerState.Reconnecting, PlayerState.Playing, PlayerState.Stopped];
        Assert.Equal(states, _stateChanges);
    }

    [Fact]
    public void FailedRoundsChangeTheStateOnce()
    {
        _player.Play(Station(Primary));
        Advance(1);
        Advance(2);

        Assert.Equal([PlayerState.Connecting, PlayerState.Reconnecting], _stateChanges);
    }

    [Fact]
    public void AnotherStationIsAChange()
    {
        _http.Hang(Primary);
        _http.Hang(Other);
        _player.Play(Station(Primary));
        var other = Station(Other);

        _player.Play(other);

        Assert.Equal([PlayerState.Connecting, PlayerState.Connecting], _stateChanges);
    }

    [Fact]
    public void StateChangesThroughAnInterruption()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);

        _player.Stop(StopKind.Interruption);
        _player.EndInterruption();

        PlayerState[] states = [PlayerState.Connecting, PlayerState.Playing, PlayerState.Interrupted, PlayerState.Connecting];
        Assert.Equal(states, _stateChanges);
    }

    [Fact]
    public void StopsThatChangeNothingAreNotChanges()
    {
        _player.Stop(StopKind.Interruption);
        _player.Stop(StopKind.User);
        _player.NetworkChanged();

        Assert.Empty(_stateChanges);
    }

    [Fact]
    public void NetworkChangeWhileConnectingIsNotAChange()
    {
        _http.Hang(Primary);
        _player.Play(Station(Primary));

        _player.NetworkChanged();

        Assert.Equal([PlayerState.Connecting], _stateChanges);
    }

    [Fact]
    public void UnsupportedStationIsAChange()
    {
        _http.Stream(Primary, "audio/flac");
        _player.Play(Station(Primary));

        _engine.Streams[0].RejectFormat();

        Assert.Equal([PlayerState.Connecting, PlayerState.Unsupported], _stateChanges);
    }

    [Fact]
    public void SleepTimerRunningOutIsAChange()
    {
        _http.Stream(Primary);
        _player.Play(Station(Primary));
        Plays(Primary);
        _player.SetSleepTimer(TimeSpan.FromMinutes(1));

        Listen(Primary, 60);

        Assert.Equal([PlayerState.Connecting, PlayerState.Playing, PlayerState.Stopped], _stateChanges);
    }

    private static Station Station(params Uri[] streams) =>
        new() { Name = "Station", PrimaryStream = streams[0], AlternateStreams = streams[1..] };

    // The newest connection to the stream sends audio, and its audio plays.
    private void Plays(Uri url)
    {
        _http.LastBody(url).Send([1]);
        _engine.Streams[^1].Play();
    }

    // Data keeps arriving on the newest connection to the stream.
    private void Listen(Uri url, int seconds)
    {
        for (var second = 0; second < seconds; second++)
        {
            Advance(1);
            _http.LastBody(url).Send([1]);
        }
    }

    // A stream with ICY metadata after every 4 bytes of audio, which plays.
    private Station PlayIcy()
    {
        _http.Stream(Primary, metaInt: 4);
        var station = Station(Primary);
        _player.Play(station);
        SendIcy();
        _engine.Streams[^1].Play();
        return station;
    }

    // 4 bytes of audio on the newest connection, then a metadata block: a title, or no update.
    private void SendIcy(string? title = null)
    {
        byte[] block = [0];
        if (title is not null)
        {
            var text = Encoding.UTF8.GetBytes($"StreamTitle='{title}';");
            var length = (text.Length + 15) / 16;
            block = new byte[1 + length * 16];
            block[0] = (byte)length;
            text.CopyTo(block, 1);
        }
        _http.LastBody(Primary).Send([1, 1, 1, 1, .. block]);
    }

    // Decoding reaches all the newest stream's audio, and the player checks on it.
    private void Decode()
    {
        var stream = _engine.Streams[^1];
        stream.Decoded = stream.Written.Length;
        Advance(0.25);
    }

    private void Advance(double seconds)
    {
        var step = TimeSpan.FromSeconds(seconds);
        _time.Advance(step);
    }

    private double[] RequestTimes(Uri? url = null) =>
        _http.Requests
            .Where(request => url is null || request.Url == url)
            .Select(request => (request.At - _start).TotalSeconds)
            .ToArray();
}
