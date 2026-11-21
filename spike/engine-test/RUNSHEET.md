# Engine test run sheet (#8)

A throwaway test of BASS on a real Android phone, and on Windows for the Windows-only checks.
This branch is never merged.

- `EngineTest/`: the test app, MAUI, Android and Windows. BASS is downloaded from un4seen at
  build time into `obj/`, never committed.
- `relay/relay.cs`: a small server for the PC. It forwards a real station and closes, stalls,
  pauses, answers 404 or a login page on command, and can freeze an HLS playlist.

## Setup

1. Build and install the APK:
   ```
   cd spike\engine-test\EngineTest
   dotnet publish -f net10.0-android -c Release
   adb install -r bin\Release\net10.0-android\publish\com.neradio.enginetest-Signed.apk
   ```
   Or copy that APK to the phone and open it. Allow notifications when asked.
2. For the relay scenarios, connect the phone by USB and run on the PC:
   ```
   adb reverse tcp:8765 tcp:8765
   cd spike\engine-test\relay
   dotnet run relay.cs
   ```
   Then type `localhost` in the app's relay address field. This keeps the relay on the PC's
   localhost: no firewall rule, nothing exposed on the network. Without USB, run
   `dotnet run relay.cs -- --lan` instead, allow it in Windows Firewall on a private network
   only, and type the PC's IP address.
   The HLS freeze happens once per relay run: restart the relay to repeat it.
3. The log: **Share log** in the app, or over USB from
   `Android/data/com.neradio.enginetest/files/engine-test-<time>.log`. One file per app start.
   Recordings are in `.../files/Recordings`. Please attach the logs to #8.

Leave **Buffer** at 5000 ms and **Pre-buffer** at 75% unless a step says otherwise. 75% is
BASS's default. #8 expects the thresholds to end up low, so if a 5-second target fails at 75%,
repeat it at 25% before recording a fail: at 75% the start alone takes about 3 s.

## Step 0: a 5-minute smoke run

The app has only run on Windows so far. Before the long test, play *MP3*, *HE-AAC* and
*HLS AAC* for a minute each, stop, then share the log. If anything there is broken, it's the
test app's fault, not BASS's, and gets fixed first.

## What the log lines mean

| Category | Meaning |
|---|---|
| `start` | Phone, Android version, battery optimization, BASS and add-on versions |
| `connect` | Each attempt: how long it took, the format, the HTTP status and content type, or the BASS error and how the app counted it |
| `buffer` | A connection starts playing once its buffer reaches the pre-buffer level, or after 10 s with `TIMEOUT`; a buffer that ran empty |
| `switch` | Which connection plays now, and whether it took over in the same mix (no gap) |
| `lost` | A lost connection: the download ended, or no new HLS segment in time |
| `silence` | Every silence over 100 ms that the listener hears, with its length |
| `audio` | Time from pressing Play, or from a network change, until audio is heard |
| `title` | Each title the stream reports, and whether it counts as a change |
| `network` | Each connectivity event: count them per Wi-Fi to cellular switch |
| `media`, `android` | Media buttons and the media session; the foreground service, its locks, and whether Android refused to start it |
| `beat` | Every 10 s while playing: state, audio output so far, total silence, buffer level, spectrum frame rate, battery, network. A missing heartbeat means the app was killed. `output` must grow by about 10 s per beat: if it doesn't, Android stopped taking audio, and silence isn't counted then. |
| `crash` | The app crashed, with the exception. A log that just ends without one means Android killed the app. |
| `burst` | The buffer during the first 4 s after a connection: how much the server sends at once |
| `record` | Each recorded file |
| `error` | Anything unexpected; please report these |

## Checks

Each check names the station to pick in the app, what to do, and what passes.

### Stream formats and plain http

1. Play each of these for a minute: *MP3*, *AAC-LC*, *HE-AAC*, *Ogg Vorbis, plain http*,
   *Opus*, *HLS AAC*.
   Pass: `connect #1: connected`, then `buffer #1 plays`, and no `silence` line after that.
   *HE-AAC* shows `=> HE-AAC`.
2. *MP3, plain http*: plays the same way. Then press **Test the app's own plain http**.
   Expected: `app's own plain http request failed: ... Cleartext ...`. That shows Android's
   cleartext block is in force for the app's own requests. So if BASS still plays plain http,
   its own connections don't need the cleartext setting.

### Server errors and login pages

3. *HTTP 404* and *Web page*: `failed ... => failed connection`, then retries 1, 2, 4... s
   apart. It must not say `format not supported`.
4. *Unsupported format: FLAC*: `=> format not supported`, and playback stops.
5. *Relay: closes after 60 s, then 404 for 30 s*, and *... then a login page for 30 s*: after
   the close, failed connections for 30 s, then `connected` again without touching the phone.

### Track titles

6. *MP3* and *Ogg Vorbis* until the track changes: a `title "..." CHANGE` line for each new
   track. A repeated title says `no change`.

### Lost connections and buffered audio

7. *Relay: server closes every 60 s*, for 5 minutes: each close shows `lost`, a `connect`,
   then `switch ... took over in the same mix`, and no `silence` line around it.
8. *Relay: stream stalls after 60 s*: `buffer ... is empty`, then `lost` about 10 s after the
   data stopped, then a reconnect. Silence of about 10 s is expected here.
9. *Relay: 9 s pause after 60 s*: no `lost` line.
10. *HLS AAC*, for 5 minutes: no `lost` lines. FIP's segments are 4 s long.
11. *Relay: HLS, no new segment for 30 s after 60 s*:
    `lost ... no new HLS segment for 14 s (limit 10 + 4 s)`, then a reconnect.

### Network changes

12. *MP3* on Wi-Fi, then turn Wi-Fi off, so the phone moves to cellular.
    Pass: no `silence` line over 5000 ms. Note how many `network` lines the switch produced.
    Turn Wi-Fi back on and check the same way.
13. *MP3*, airplane mode on for a minute, then off.
    Pass: `audio audible N ms after network change` with N at most 5000. N counts from the
    first `network` event with access `Internet`, not from turning airplane mode off.

### Background playback and media controls

14. *MP3*, phone unplugged, screen off, for 30 minutes.
    Pass: a `beat ... playing` line every 10 s throughout, battery `Discharging`, and no
    unexpected `silence`. At the end, the lock-screen controls still stop and play.
15. While it plays, switch to another app for a few minutes: the beats go on.
16. Lock screen, when a title changes: it shows the new title.
17. Headset or Bluetooth play/pause button: `media button ...`, `stop by media session pause`.
    Press again: `play again, by media session play`.
18. Headset button after a stop, Android 12 and later: stop, put the app in the background,
    screen off, wait 2 minutes, press play on the headset.
    Record what happens: `foreground service start requested` and the station plays, or
    `foreground service start refused: ...`.

### Equalizer, spectrum, recording

19. Move the equalizer sliders while playing: the sound changes at once, without glitches.
20. Spectrum: with the screen on, the beats show `fft 60 fps, 60 with new data` or close to
    it. The app doesn't ask for the microphone permission. Its merged manifest has no
    `RECORD_AUDIO`, which was checked at build time.
21. While *MP3* plays, turn **Record** on and wait for two track changes; then the same with
    *Ogg Vorbis*. Expected: a `record new file` line per title. Copy the files to a PC: each
    one plays and holds one track, with edges off by a few seconds at most.
    MP3 and AAC split at ICY title changes, and a reconnect with the same title stays in the
    same file. Ogg splits where a new Ogg stream starts, so a reconnect also starts a new
    file, and an Ogg file can be named after the previous title.
    Already checked on the PC: MP3 and Ogg files from a stream with reconnects all decode.

### Buffer size (Q30)

22. Set **Buffer** to 60000 and **Pre-buffer** to 5, then play each station from step 1 once.
    The `burst` lines show how many seconds each server sends at once on connecting.
    Do it again for one station after a relay close (step 7).
23. With the defaults, note `audio audible N ms after user played` for each station. That's
    the start delay that the pre-buffer level costs.

### Windows (run the app on a PC: `dotnet run -f net10.0-windows10.0.19041.0`)

24. Minimize the app while it plays: the beats go on.
25. The keyboard's play/pause media key: `Windows media button Pause`, then `Play`.
26. Windows' media flyout shows the station and the title.
27. Play through USB or Bluetooth headphones and disconnect them: playback carries on through
    the speakers, with no long `silence`.

## Already found on the PC, before the phone test

- **AAC is a problem on Windows.** Observed on this PC, a regular Windows 11 26H2 (build
  26300.9974, the November 2026 cumulative update, not an Insider build): BASS decodes AAC
  through Media Foundation, and `BASS_StreamCreateURL` returns only once exactly 1 MiB
  (1,048,576 bytes) has been downloaded. That's 50 s at 128 kbps, 99 s at 64 kbps and 200 s at
  32 kbps. HLS with AAC fails with `FileFormat`, because FIP's live playlist never holds 1 MiB.
  Windows' own `MediaPlayer`, also Media Foundation, opens the same streams in 1-2 s, HLS
  included, so the wait is in how BASS hands the stream to Media Foundation.
  `BASS_CONFIG_VERIFY`, `VERIFY_NET` and `NET_PREBUF_WAIT` make no difference. BASS_AAC would
  bypass Media Foundation, but it's built on FAAD2, which is GPL. Android decodes AAC with its
  own codecs and isn't affected.
- MP3, Ogg Vorbis and Opus play. Titles arrive from ICY and from Ogg comments. The Opus
  station tried sends no title, so Opus titles are unchecked.
- An HTTP 404 gives `FileOpen`. A web page and a real unsupported format both give
  `FileFormat`. So the app tells them apart by the content type, which BASS reports. A
  connection that closes before the server answers also gives `FileFormat`, with no HTTP
  status, so that counts as a failed connection too.
- A server close fires BASS's "download ended" sync, and BASS doesn't reconnect a live
  stream by itself. On Windows a stall is caught by BASS's read timeout (`NET_READTIMEOUT`, set
  to 10 s). On Android that timeout doesn't fire in this app (see below), so the app has its
  own watchdog: no new data for 10 s counts as a lost connection.
- **BASS's pre-buffer doesn't apply to decoding streams in a mixer.** They play as soon as
  any data arrives. So the app holds a new connection paused until its buffer reaches the
  pre-buffer level. #7's engine seam needs the same.
- With that, a server close is covered by the buffered audio. The new connection takes over
  in the same mix, with no silence.
- HLS: BASSHLS doesn't give the playlist's target duration. It gives each segment's `EXTINF`
  and a sync when a segment starts downloading, so the stall limit uses the segment duration.
  By default BASSHLS starts 30 s behind live.
- The Android media session is the platform's own `android.media.session.MediaSession`, so no
  library is needed.

## Found on the phone (Galaxy A73, Android 16, 21 Nov)

- **HTTPS needs the BASS_SSL add-on on Android.** From Android 7 on, apps can't load the
  system's OpenSSL, and without the add-on every `https://` stream fails at once with `SSL`.
  The build fetches it. It's based on OpenSSL 1.1.1w, which is out of support and needs an
  attribution notice: #7 has to decide on that.
- **AAC and HLS AAC play on Android**, so the 1 MB AAC problem above is Windows-only.
- **A .NET GC during `BASS_StreamCreateURL` makes BASS drop the attempt.** It hangs up before
  any HTTP status, with `FileOpen` or `Timeout`, in 15-45% of attempts. A GC forced 250 ms into
  each attempt made 20 of 20 fail; `curl` on the phone and the Windows app never fail this way.
  `MONO_THREADS_SUSPEND=hybrid` doesn't help. Opening the stream on a plain Java thread, through
  BASS's own Java API, does: with the same GC forced into every attempt, 20 of 20 connected.
  The app retries a dropped attempt at once instead, up to 3 times, without moving on in the
  waits.
- **BASS's read timeout fires on Android only without a download callback and on a stream
  that isn't chunked.** Against a plain HTTP/1.0 server on the phone's own loopback it fires
  (with a 5 s or a 10 s timeout, from a .NET or a Java thread); with the app's download callback
  set, or through the relay's chunked stream, it never does. The app needs the callback for
  recording, so it keeps its own watchdog.
- **A page that isn't audio, over plain http, held each attempt for 30 s.** When BASS's own
  decoders reject a stream, BASS hands the URL to Android's media extractor, which fetches it
  again with the platform's HTTP stack; the cleartext policy blocks that, and Android retries
  for 30 s. `BASS_CONFIG_AM_DISABLE = 2` turns that fallback off but keeps Android's AAC
  decoder: a Wi-Fi login page now fails in milliseconds.
- **Servers send a fixed amount at once on connecting, not a fixed duration** (check 22):
  SomaFM and Radio Paradise send about 256 KB, so 11 s at 192 kbps but 35 s at 64 kbps; DLF's
  Opus about 80 KB. BASSHLS starts 30 s behind live anyway.
- **Recording:** a title can hold characters Windows doesn't allow in a file name, so names
  follow Windows' rules; and an Ogg recording switched on mid-stream waits for the next stream,
  since without the stream's header pages it can't be decoded.
- **BASSHLS starts each connection 30 s behind live** (`BASS_CONFIG_HLS_DELAY`) and can't seek in
  a live stream, so after an HLS reconnect up to 30 s already heard play again. #7 needs to skip
  the new connection forward to the first segment not yet played.
- **BASSHLS doesn't recognize a chunked playlist.** The relay sends playlists with a length.
- **`ChannelGetInfo().Plugin` doesn't name BASSHLS on Android**, so the app recognizes HLS by
  its `EXTINF` tag.

## BASS problems, in short

| Platform | Problem | Status |
|---|---|---|
| Windows | AAC opens only after 1 MiB has been downloaded (50-200 s); HLS AAC doesn't play at all | Open, on the current Windows 11. Way around: decode AAC/HLS with Windows' own media stack and feed the audio into BASS |
| Android | A .NET GC during connect drops 15-45% of attempts | Mitigated: the app retries at once. Proven fix: open streams on a Java thread |
| Android | The read timeout doesn't catch a stall with a download callback set, or on a chunked stream | Worked around: the app's own stall watchdog |

Also for #7, though not bugs of the same kind: BASS_SSL is built on OpenSSL 1.1.1w, which is out
of support, and it needs an attribution notice; and BASSHLS replays up to 30 s after a reconnect.
The 30 s wait on plain-http pages is fixed with `BASS_CONFIG_AM_DISABLE = 2`.
