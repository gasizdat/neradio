# Design

## Context

There's no code yet. The project skeleton (#9) creates a core library with no MAUI dependency, the app, and a test project. The audio engine is chosen by the engine test on a real Android phone (#8), and so is the buffer size (Q30). For the motivation, see proposal.md (Why). For the behavior, see `specs/playback/spec.md` and `specs/sleep-timer/spec.md`.

## Goals / Non-Goals

**Goals:**
- Every playback decision that depends on neither the engine nor the platform lives in one place in the core library, and is tested on fake time.
- The engine chosen in #8 plugs in without changing that logic.
- Every scenario is checked: by a unit test where it depends on neither the engine nor the platform, and by hand on a device where it does. Some are checked both ways, such as "Network changes", whose rounds are unit-tested and whose 5-second targets are checked on a phone.

**Non-Goals:**
- Choosing the engine and the buffer size (#8).
- The player screen (#10).

## Decisions

### Playback logic in the core library
One player component in the core library owns all of this:
- the playback state: stopped, connecting, playing, reconnecting, and interrupted, for a station stopped by a call or the like that plays again when it ends;
- connection rounds, the waits between them, and stall detection;
- restarts on network changes;
- skipped streams;
- track titles;
- the sleep timer.

The platform code reports each stop by its kind: the user's, an interruption, or another stop by the system, such as another app taking over. Only the user's stop cancels the sleep timer, and only an interruption ends with the station playing again. Any stop during an interruption, including the sleep timer running out, ends it without playing again. Playing again keeps the last title, as a reconnect does, so the same title isn't a change after every call.

It takes time from .NET's `TimeProvider`. Tests then run on a fake one, and the 30-second waits and the 30-minute timer take no real time. The fake comes from `Microsoft.Extensions.TimeProvider.Testing` (MIT), a dependency of the test project only.

Alternative: the logic in the platform code of the app. Rejected: it would be written twice, once for Windows and once for Android, and its timing could only be tested with real waits.

### One seam to the engine
The player talks to the engine through one interface. The interface:
- opens a stream URL;
- stops a stream;
- sets the volume, for lowering it during prompts;
- reports events: audio data arrived, the connection ended or failed, the format isn't supported, and title metadata arrived.

It has two implementations: an adapter for the engine chosen in #8, and a fake in the tests. That isn't speculative. The engine isn't chosen yet, and without the fake, testing the logic would need the network and a device.

The seam lets the player open a new connection while the old one's buffered audio still plays, and switch when that audio runs out ("Buffered audio while reconnecting"). Whether the engine can switch without a gap is one of #8's checks.

Alternative: call the engine directly. Rejected: the logic couldn't be tested without the network, and a change of engine after #8 would touch it.

### Unsupported means audio the engine can't decode
A skipped stream isn't tried again until playback stops, so the adapter reports "the format isn't supported" only for audio the engine can't decode. An HTTP error status, such as a 404 while a station's server restarts, and a response that isn't audio, such as a Wi-Fi login page, are reported as failed connections, and the rounds go on. #8 checks how the engine tells them apart.

Alternative: try skipped streams again after a network change, for login pages. Rejected: once web pages count as failed connections, whether a format can be played doesn't depend on the network.

### One 10-second limit for stalls and for attempts
Q29's 10 seconds without data counts a playing connection as stalled. The same limit also fails a connection attempt that hasn't produced audio data. This one rule bounds the time a round spends on a dead primary.

For HLS, the limit is 10 seconds plus the playlist's target duration, since data arrives a segment at a time. The target duration has to reach the player. The seam then reports it with the data events, or the player reads it from the media playlist itself; #8 shows which of the two the engine allows.

Alternative: a separate, shorter connect timeout. Rejected: a slow but working stream would then fail.

### Playlist URLs resolved in the core library, by extension
The player downloads a URL whose path ends in `.pls`, `.m3u` or `.m3u8` with `HttpClient`, with a small size cap and the same 10-second limit as a connection attempt, each time a round reaches it, and parses it:
- PLS: the `FileN=` entries, in order of N;
- M3U: the lines that aren't comments.

A playlist isn't kept between rounds, and a download that fails counts as a stream that failed to connect. The stream that played last is remembered by its own URL, so the next round can start with it without downloading the playlist again.

An `.m3u8` with `#EXT-X-` tags is HLS, and its URL goes to the engine as a stream. Every other URL goes straight to the engine.

Alternative: the engine's own playlist handling. Rejected: the engine isn't chosen yet, and the rounds need the entries, to try them in order.

Alternative: check the content type of every stream URL. Rejected: it costs an extra connection per stream. Feature 12 asks for `.pls` and `.m3u` playlist URLs, and the extension covers those.

### ICY titles: UTF-8, else Windows-1252
ICY metadata (`StreamTitle='…';`) declares no text encoding. The player reads the title as UTF-8 when the bytes are valid UTF-8, and as Windows-1252 otherwise. Ogg comments are UTF-8 by definition. Windows-1252 comes from .NET's code-pages encoding provider, which ships with .NET, so no package is added.

Alternative: Latin-1 as the fallback. Rejected: titles from Windows software often have curly quotes and dashes, which are control characters in Latin-1. Windows-1252 reads every other byte the same way Latin-1 does.

### Network changes from MAUI's `Connectivity`
The app forwards MAUI's `Connectivity.ConnectivityChanged` event to the player on both platforms. The player then starts the waits over, with a round at once, as in "Network changes".

Alternative: each platform's own network callbacks. Rejected, unless #8 finds that MAUI's event misses a switch from Wi-Fi to cellular. #8 also counts how many events one such switch fires.

### Android
- **Foreground service:** a foreground service of the media-playback type runs while a station plays, reconnects or is interrupted.
  - Android 12 and later let an app start a foreground service from the background only in some cases. A media button press and a tap on the notification are among them, but getting audio focus back after a call isn't. So the service stays in the foreground through an interruption.
  - After any other stop, the service leaves the foreground, and the media session stays active, so the notification and the headset buttons can play the station again. #8's "Headset button" check confirms this on the phone.
- **Media session:** a media session drives the notification, the lock screen and headset and Bluetooth buttons. #8 picks the library for it, since its test app needs one too. It must not be GPL.
- **Locks:** the app holds a partial wake lock and a Wi-Fi lock while playing or reconnecting, so the CPU and Wi-Fi don't sleep with the screen off.
- **Audio focus:** the app requests audio focus when playback starts.
  - A temporary loss, such as a call, an alarm or a voice assistant, is an interruption: playback stops, and the station plays again when focus returns.
  - A loss that allows ducking, such as a prompt, lowers the volume.
  - A permanent loss stops playback.
- **Output changes:** the "audio becoming noisy" broadcast stops playback.
- **Plain http:** the manifest allows cleartext traffic. That covers the app's own requests, such as playlist downloads. #8 checks whether the engine's own connections also need it.
- **Permissions:** a foreground service of the media-playback type, posting notifications, and wake locks.

### Windows
- The system media transport controls handle the media keys and Windows' media controls.
- The app keeps playing when minimized.
- Playback follows the default output device. #8 checks how the engine does that.
- Calls and prompts need nothing from the app: Windows lowers other sounds during a call on its own.

### What #8 checks, and how the rest is verified
The engine test checks by hand on the phone what depends on the engine. These are the scenarios of:
- "Stream formats" and "Plain-http streams";
- "Server answers with an error" and "Wi-Fi login page" of "Unsupported formats";
- "Track titles", that titles arrive;
- "Lost connections" and "Network changes";
- "Buffered audio while reconnecting";
- "Background playback" and "System media controls".

The rest of the pass list is the equalizer, visualizer and recording criteria in Decisions.

In the implementation, the app logic is pinned by unit tests in the core library, running on the fake engine and fake time. That covers:
- "One station at a time" and "Stopping a live stream";
- "Playlist URLs" and "Unsupported formats";
- "Track titles" and "Repeated titles aren't changes";
- "Lost connections", "Connection rounds", "Waits between rounds" and "Network changes";
- the player's part of "Phone calls and other interruptions";
- the sleep timer.

Scenarios that depend on the engine or the platform are checked by hand on an Android phone and on Windows, as tasks 3 to 5 list them:
- stream formats, plain http, server errors and login pages, titles from real streams, HLS segments, and buffered audio;
- background playback and system controls;
- interruptions, prompts, another app taking over, and output changes;
- the 5-second network targets;
- the sleep timer with the screen off and during a call.

Each implementation PR lists each one with its result.

This departs from the `neradio-surgical-change` rule that every scenario is pinned by a test, so the PR calls it out. Alternative: Android device tests on an emulator in CI. Rejected for these scenarios: an emulator doesn't reproduce power saving, calls or real network switches, which is also why #8 uses a real phone.

## Risks / Trade-offs

- [Some phone makers' battery savers kill foreground services despite Android's rules] → #8 runs on the owner's own phone. If it happens there, a later change guides the user to exempt the app.
- [Android ends the app a while after a stop in the background] → The headset buttons no longer play the station until the user opens the app. Accepted.
- [MAUI's connectivity event can fire for a change that doesn't affect the connection in use, such as a cellular network appearing while on Wi-Fi] → The app reconnects needlessly. The buffer usually hides it, so this is accepted.
- [One switch from Wi-Fi to cellular fires several connectivity events] → Each one abandons the round under way, which delays the reconnect. If #8 shows this, the app forwards only the events that change the network access or the connection profiles.
- [A dead primary that drops packets instead of refusing the connection] → Starting can take 10 seconds before the alternate is tried. Accepted, so that slow but working streams don't fail.
- [Stations that send Cyrillic titles in Windows-1251] → The titles are garbled under the Windows-1252 fallback. A later change could pick the fallback from the station's `language`.
- [The engine reports an HTTP error or a web page as a format it can't play] → A working stream is skipped until playback stops. #8 checks it. If the engine can't tell them apart, the adapter reads the response's status and content type itself.
- [A playlist URL without a `.pls`, `.m3u` or `.m3u8` extension] → It goes to the engine as a stream. A playlist isn't audio, so it counts as a failed connection, and the app keeps retrying without ever playing it. Accepted as rare: catalogs usually list resolved stream URLs.
- [The engine can't switch from the old connection's buffer without a gap] → Short drops cause a short silence. If #8 shows this, the app feeds the audio to the output itself, at the cost of more code.
- [A title arrives when its data is downloaded, ahead of the audio by up to the buffer's length] → The shown title changes a few seconds early. Recording (#13, Q26) decides whether to delay it.
- [Reconnecting never gives up, with the wake lock held] → A long outage drains the battery: at most one round every 30 seconds. Accepted, following Q28; Stop and the sleep timer end it.
- [A live stream can't replay what it broadcast during a drop] → After a reconnect, the station goes on from the live broadcast. That follows from live radio.
