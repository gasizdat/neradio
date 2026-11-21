# Design

## Context

There's no code yet. The project skeleton (#9) creates a core library with no MAUI dependency, the app, and a test project. The engine test (#8) chose BASS, through ManagedBass, and set the buffer (Q30): playback starts once about 4 seconds of audio are buffered, and the buffer holds up to 60 seconds. It also found problems in BASS's own connections, listed in [#8's summary](https://github.com/gasizdat/neradio/issues/8#issuecomment-7064789810), which shape the decisions below. For the motivation, see proposal.md (Why). For the behavior, see `specs/playback/spec.md` and `specs/sleep-timer/spec.md`.

## Goals / Non-Goals

**Goals:**
- Every playback decision that depends on neither the engine nor the platform lives in one place in the core library, and is tested on fake time. That includes the connections to the station.
- BASS plugs in behind one seam, and only decodes and mixes.
- Every scenario is checked: by a unit test where it depends on neither the engine nor the platform, and by hand on a device where it does. Some are checked both ways, such as "Network changes", whose rounds are unit-tested and whose 5-second targets are checked on a phone.

**Non-Goals:**
- The player screen (#10).

## Decisions

### Playback logic in the core library
One player component in the core library owns all of this:
- the playback state: stopped, connecting, playing, reconnecting, and interrupted, for a station stopped by a call or the like that plays again when it ends;
- the connections: the HTTP request, the ICY titles in the stream, and HLS segments;
- connection rounds, the waits between them, and stall detection;
- restarts on network changes;
- skipped streams;
- track titles;
- the sleep timer.

The platform code reports each stop by its kind: the user's, an interruption, or another stop by the system, such as another app taking over. Only the user's stop cancels the sleep timer, and only an interruption ends with the station playing again. Any stop during an interruption, including the sleep timer running out, ends it without playing again. Playing again keeps the last title, as a reconnect does, so the same title isn't a change after every call.

It takes time from .NET's `TimeProvider`. Tests then run on a fake one, and the 30-second waits and the 30-minute timer take no real time. The fake comes from `Microsoft.Extensions.TimeProvider.Testing` (MIT), a dependency of the test project only.

Alternative: the logic in the platform code of the app. Rejected: it would be written twice, once for Windows and once for Android, and its timing could only be tested with real waits.

### The app makes every connection; BASS only decodes
The core library downloads every stream itself and passes its bytes to the engine, which decodes and mixes them. BASS opens no connection.
- The request asks for ICY metadata (`Icy-MetaData: 1`), and the core takes the metadata blocks out of the stream before passing it on. It knows the HTTP status, the content type, and when each byte arrives.
- `HttpClient` runs on a `SocketsHttpHandler`, created explicitly. .NET's default handler on Android is the platform's HTTP stack, which Android's cleartext policy blocks for plain http. `SocketsHttpHandler` isn't subject to it, so plain-http stations play with no manifest setting.

#8 found these problems in BASS's own connections:
- On Android, a .NET GC during `BASS_StreamCreateURL` drops 15–45% of attempts before any HTTP answer.
- On Android, BASS's read timeout never fires with a download callback set, which recording needs, or on a chunked stream.
- On Windows, AAC opens only after 1 MiB has arrived, 50–200 seconds, and HLS AAC doesn't play at all.
- BASSHLS starts 30 seconds behind live and can't seek, so an HLS reconnect replays up to 30 seconds.
- https on Android needs BASS_SSL, which is built on OpenSSL 1.1.1w, a version out of support.

None of these apply when the app makes the connections, and the app ships neither BASS_SSL nor BASSHLS. #8's [push check](https://github.com/gasizdat/neradio/blob/drafts/ai/engine-test/spike/engine-test/RUNSHEET.md#the-app-owning-every-connection-push-check) showed it on the phone: MP3, AAC-LC, HE-AAC, Ogg Vorbis and Opus decoded from pushed bytes with a GC forced every 50 ms. With the same GCs, 20 of 20 of BASS's own attempts failed. It also gives recording (#13) the stream's raw bytes without a BASS callback.

Alternative: BASS's own connections, with a workaround for each problem: immediate retries or a Java-thread opener on Android, a stall watchdog, and the app's own connection for AAC on Windows only. Rejected: it takes three fixes for one cause. The Java-thread opener loses the download callback that recording needs. The HLS replay stays, and the out-of-support OpenSSL ships in the app.

### HLS in the core library
An HLS playlist is read by the core:
- From a master playlist, it takes the variant with the highest bandwidth.
- It reloads the media playlist as RFC 8216 asks, and downloads each segment once, in order of media sequence number.
- It starts as far back as the playlist lists, up to the buffer's 60 seconds, so the buffer fills at once, as an Icecast server's burst does.
- After a reconnect, it goes on with the segment that follows the last one it downloaded, or from the newest segments when the playlist has moved past it ("No repeated audio after an HLS reconnect").
- It takes the AAC (ADTS) out of MPEG-TS segments, and passes packed-audio segments on without their ID3 header. Segments in fragmented MP4 count as a format the app can't play.
- The target duration for the stall limit comes from the media playlist.

Alternative: BASSHLS. Rejected: it starts 30 seconds behind live and replays that much after a reconnect, and with the app's own connections it isn't needed.

### One seam to the engine
The player talks to the engine through one interface. The interface:
- opens a decoder for a stream, given its content type, and takes the stream's bytes as they arrive;
- stops a stream;
- sets the volume, for lowering it during prompts;
- reports events: the stream's audio started playing, its buffered audio ran out, the format isn't supported, and an Ogg stream's title changed;
- reports how far decoding has got in the stream's bytes, so a title can wait for its audio.

It has two implementations: the BASS adapter, and a fake in the tests. Without the fake, testing the logic would need a device.

The seam lets the player open a new connection while the old one's buffered audio still plays, and switch when that audio runs out ("Buffered audio while reconnecting"). #8 showed BASSmix doing that without a gap.

Alternative: call BASS directly. Rejected: the logic couldn't be tested without a device.

### The BASS adapter
- Each connection's bytes go into a BASS user stream (`STREAMFILE_BUFFERPUSH`): a decoding channel in one BASSmix mixer, which plays. The equalizer and the visualizer (#14) go on the mixer, so they stay across reconnects.
- BASS decodes MP3 and Ogg Vorbis itself, and Opus through BASSOPUS.
- On Android, BASS hands AAC to Android's own codec. `BASS_CONFIG_AM_DISABLE` is set to 2, so Android's codecs decode only AAC. Otherwise BASS would hand them every stream its own decoders reject, and Android 9 and later would play formats that Windows doesn't, such as FLAC.
- On Windows, BASS would hand AAC to Media Foundation and wait for 1 MiB. So the adapter decodes the ADTS frames itself with Windows' own AAC decoder, a Media Foundation transform, through the bindings in NAudio.Wasapi (MIT). It pushes the audio into a BASS push stream in the same mixer. The decoder changes its output format after an HE-AAC stream's first frames, when it finds the doubled sample rate. #8's `windecode` check reached the start level in 0.9–3.1 seconds for AAC-LC, HE-AAC and FIP's HLS.
- BASS's pre-buffer doesn't apply to decoding channels in a mixer: they play as soon as any data arrives. So the adapter keeps a new connection paused in the mixer until about 4 seconds of its audio are buffered, and only then switches to it (Q30).
- Each connection's buffer holds 60 seconds of audio (Q30). While it's full, the core stops reading from the connection.
- BASS, BASSmix and BASSOPUS are fetched at build time and never committed.

Alternative: BASS_AAC for AAC on Windows. Rejected: it's built on FAAD2, which is GPL.

### Unsupported means audio the engine can't decode
A skipped stream isn't tried again until playback stops, so only audio the engine can't decode counts as unsupported. The core knows the HTTP status and the content type from its own request. An HTTP error status, such as a 404 while a station's server restarts, and a response whose content type isn't audio, such as a Wi-Fi login page, are failed connections, and the rounds go on. #8 found that a login page and audio in an unknown format both look like `BASS_ERROR_FILEFORM` to BASS, so the content type is what tells them apart.

Alternative: try skipped streams again after a network change, for login pages. Rejected: once web pages count as failed connections, whether a format can be played doesn't depend on the network.

### One 10-second limit for stalls and for attempts
Q29's 10 seconds without data counts a playing connection as stalled. The same limit also fails a connection attempt that hasn't produced audio data. This one rule bounds the time a round spends on a dead primary. The core counts the bytes arriving on the connection itself, so BASS's read timeout isn't used.

For HLS, the limit is 10 seconds plus the playlist's target duration, since data arrives a segment at a time.

Alternative: a separate, shorter connect timeout. Rejected: a slow but working stream would then fail.

### Playlist URLs resolved in the core library, by extension
The player downloads a URL whose path ends in `.pls`, `.m3u` or `.m3u8` with `HttpClient`, with a small size cap and the same 10-second limit as a connection attempt, each time a round reaches it, and parses it:
- PLS: the `FileN=` entries, in order of N;
- M3U: the lines that aren't comments.

A playlist isn't kept between rounds, and a download that fails counts as a stream that failed to connect. The stream that played last is remembered by its own URL, so the next round can start with it without downloading the playlist again.

An `.m3u8` with `#EXT-X-` tags is HLS, which the core plays as above. Every other URL is a stream.

Alternative: the engine's own playlist handling. Rejected: the rounds need the entries, to try them in order.

Alternative: check the content type of every stream URL. Rejected: it costs an extra connection per stream. Feature 12 asks for `.pls` and `.m3u` playlist URLs, and the extension covers those.

### ICY titles: UTF-8, else Windows-1252
ICY metadata (`StreamTitle='…';`) declares no text encoding. The player reads the title as UTF-8 when the bytes are valid UTF-8, and as Windows-1252 otherwise. Ogg comments are UTF-8 by definition. Windows-1252 comes from .NET's code-pages encoding provider, which ships with .NET, so no package is added.

Alternative: Latin-1 as the fallback. Rejected: titles from Windows software often have curly quotes and dashes, which are control characters in Latin-1. Windows-1252 reads every other byte the same way Latin-1 does.

### Titles wait for their audio
On connecting, servers send up to 35 seconds of audio at once (#8), so a title can arrive that far ahead of its audio. The core notes where in the stream's bytes each ICY title came. It shows the title, and counts the change, once the engine reports that decoding has reached that point. An Ogg title comes from BASS when decoding reaches the new Ogg stream. Both are then off by the mixer's playback buffer, a fraction of a second.

Alternative: show each title as it arrives. Rejected: the title would change up to 35 seconds early, and the track history (#12) and playlists (#11) would act on the wrong track.

### Network changes from MAUI's `Connectivity`
The app forwards MAUI's `Connectivity.ConnectivityChanged` event to the player on both platforms. The player then starts the waits over, with a round at once, as in "Network changes".

Alternative: each platform's own network callbacks. Rejected: with airplane mode, #8 saw one event when the network went and one when it came back. #8 didn't get to a switch from Wi-Fi to cellular, so task 4.3 checks it on a phone.

### Android
- **Foreground service:** a foreground service of the media-playback type runs while a station plays, reconnects or is interrupted.
  - Android 12 and later let an app start a foreground service from the background only in some cases. A tap on the notification is one of them, and so is a media button press: on Android 16, #8 started playback with the play button two minutes after a stop, with the screen off and the app in the background. Getting audio focus back after a call isn't one of them. So the service stays in the foreground through an interruption.
  - After any other stop, the service leaves the foreground, and the media session stays active, so the notification and the headset buttons can play the station again.
- **Media session:** Android's own `android.media.session.MediaSession` drives the notification, the lock screen and headset and Bluetooth buttons, as in #8's test app. No library is needed.
- **Locks:** the app holds a partial wake lock and a Wi-Fi lock while playing or reconnecting, so the CPU and Wi-Fi don't sleep with the screen off.
- **Audio focus:** the app requests audio focus when playback starts.
  - A temporary loss, such as a call, an alarm or a voice assistant, is an interruption: playback stops, and the station plays again when focus returns.
  - A loss that allows ducking, such as a prompt, lowers the volume.
  - A permanent loss stops playback.
- **Output changes:** the "audio becoming noisy" broadcast stops playback.
- **Plain http:** no manifest setting, since the core's `SocketsHttpHandler` isn't subject to Android's cleartext policy.
- **Permissions:** a foreground service of the media-playback type, posting notifications, and wake locks.

### Windows
- The system media transport controls handle the media keys and Windows' media controls.
- The app keeps playing when minimized.
- BASS plays on its "Default" device with `BASS_CONFIG_DEV_DEFAULT` on, so playback follows Windows' default output device. #8 didn't get to unplugging headphones on Windows, so task 5.2 checks it.
- Calls and prompts need nothing from the app: Windows lowers other sounds during a call on its own.

### What #8 checked, and how the rest is verified
#8 checked by hand what depends on the engine. It ran in an afternoon, on a Galaxy A73 with Android 16 and on Windows 11, instead of 2–3 days: see the [phone](https://github.com/gasizdat/neradio/issues/8#issuecomment-7064295467) and [PC](https://github.com/gasizdat/neradio/issues/8#issuecomment-6959503878) results. Its pass list passed with its test app's workarounds, except for what it left open:
- Wi-Fi to cellular (task 4.3);
- an hour unplugged with the screen off, and a real headset button (task 4.1);
- headphones unplugged on Windows (task 5.2);
- listening for equalizer glitches (#14).

#8 played streams through BASS's own connections. The connections above replace them, and #8 checked those only with its push check and `windecode`. So task 3.1 checks every engine scenario again by hand.

In the implementation, the app logic is pinned by unit tests in the core library, running on the fake engine and fake time. The connections run on a fake HTTP handler that replays recorded stream data. That covers:
- "One station at a time" and "Stopping a live stream";
- "Playlist URLs" and "Unsupported formats";
- "Track titles", "Repeated titles aren't changes" and "Titles show with their audio";
- "Lost connections", "Connection rounds", "Waits between rounds" and "Network changes";
- "No repeated audio after an HLS reconnect";
- the player's part of "Phone calls and other interruptions";
- the sleep timer.

Scenarios that depend on the engine or the platform are checked by hand on an Android phone and on Windows, as tasks 3 to 5 list them:
- stream formats, plain http, server errors and login pages, titles from real streams, HLS segments and reconnects, and buffered audio;
- background playback and system controls;
- interruptions, prompts, another app taking over, and output changes;
- the 5-second network targets;
- the sleep timer with the screen off and during a call.

Each implementation PR lists each one with its result.

This departs from the `neradio-surgical-change` rule that every scenario is pinned by a test, so the PR calls it out. Alternative: Android device tests on an emulator in CI. Rejected for these scenarios: an emulator doesn't reproduce power saving, calls or real network switches, which is also why #8 used a real phone.

## Risks / Trade-offs

- [The app takes on what BASS's connections did: HTTP, ICY, HLS and MPEG-TS] → More code to get right. Each part is in the core and unit-tested with recorded data, and task 3.1 runs it against real stations.
- [Shoutcast v1 servers answer `ICY 200 OK`, which `HttpClient` rejects] → Such stations fail to connect. The catalog (#4) shows how many stations do this. If they matter, the core reads those responses over a plain socket.
- [The variant with the highest bandwidth uses more mobile data] → FIP's highest is 280 kbps, about 125 MB an hour. Accepted for now; a later change can add a setting.
- [Some phone makers' battery savers kill foreground services despite Android's rules] → #8 played 30 minutes with the screen off on the owner's phone without it happening. If it happens later, a later change guides the user to exempt the app.
- [Android ends the app a while after a stop in the background] → The headset buttons no longer play the station until the user opens the app, and a sleep timer that was still running is lost. Accepted.
- [MAUI's connectivity event can fire for a change that doesn't affect the connection in use, such as a cellular network appearing while on Wi-Fi] → The app reconnects needlessly. The buffer usually hides it, so this is accepted.
- [One switch from Wi-Fi to cellular fires several connectivity events] → Each one abandons the round under way, which delays the reconnect. If task 4.3 shows this, the app forwards only the events that change the network access or the connection profiles.
- [A dead primary that drops packets instead of refusing the connection] → Starting can take 10 seconds before the alternate is tried. Accepted, so that slow but working streams don't fail.
- [Stations that send Cyrillic titles in Windows-1251] → The titles are garbled under the Windows-1252 fallback. A later change could pick the fallback from the station's `language`.
- [A playlist URL without a `.pls`, `.m3u` or `.m3u8` extension] → It's played as a stream, which can't work: it counts as a failed connection or as unsupported, depending on its content type. Accepted as rare: catalogs usually list resolved stream URLs.
- [Reconnecting never gives up, with the wake lock held] → A long outage drains the battery: at most one round every 30 seconds. Accepted, following Q28; Stop and the sleep timer end it.
- [A live stream can't replay what it broadcast during a drop] → After a reconnect, the station goes on from the live broadcast. That follows from live radio.
