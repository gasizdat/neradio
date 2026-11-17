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

Leave **Buffer** at 5000 ms and **Pre-buffer** at 75% unless a step says otherwise.

## What the log lines mean

| Category | Meaning |
|---|---|
| `start` | Phone, Android version, battery optimization, BASS and add-on versions |
| `connect` | Each attempt: how long it took, the format, the HTTP status and content type, or the BASS error and how the app counted it |
| `buffer` | A connection starts playing once its buffer reaches the pre-buffer level; a buffer that ran empty |
| `switch` | Which connection plays now, and whether it took over in the same mix (no gap) |
| `lost` | A lost connection: the download ended, or no new HLS segment in time |
| `silence` | Every silence over 100 ms that the listener hears, with its length |
| `audio` | Time from pressing Play, or from a network change, until audio is heard |
| `title` | Each title the stream reports, and whether it counts as a change |
| `network` | Each connectivity event: count them per Wi-Fi to cellular switch |
| `media`, `android` | Media buttons and the media session; the foreground service, its locks, and whether Android refused to start it |
| `beat` | Every 10 s while playing: state, audio output so far, total silence, buffer level, spectrum frame rate, battery, network. A missing heartbeat means the app was killed. |
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
    network event, not from turning airplane mode off.

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
21. Turn **Record** on with *MP3* for two track changes, and again with *Ogg Vorbis*:
    a `record new file` line per title. Copy the files to a PC: each one plays and holds one
    track. The edges can be off by a few seconds.

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

- MP3, Ogg Vorbis and Opus play, and their titles arrive (ICY and Ogg comments).
- **AAC is a problem on Windows.** BASS decodes AAC through Media Foundation there, and it
  reads 1 MB of the stream before it opens: 50 s at 128 kbps, 200 s at 32 kbps. HLS fails
  outright (`FileFormat`), because FIP's live playlist holds less than 1 MB. Android decodes
  AAC with its own codecs, so the phone test decides whether this is Windows-only.
- An HTTP 404 gives `FileOpen`. A web page and a real unsupported format both give
  `FileFormat`. So the app tells them apart by the content type, which BASS reports.
- A server close fires BASS's "download ended" sync, and BASS doesn't reconnect a live
  stream by itself. A stall is caught by BASS's read timeout (`NET_READTIMEOUT`, set to 10 s).
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
