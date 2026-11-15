# Design

## Context

There's no code yet. The project skeleton (#9) creates a core library with no MAUI dependency, the app, and a test project. The audio engine is chosen by the engine test on a real Android phone (#8), and so is the buffer size (Q30). For the motivation, see proposal.md (Why). For the behavior, see `specs/playback/spec.md` and `specs/sleep-timer/spec.md`.

## Goals / Non-Goals

**Goals:**
- Every playback decision that depends on neither the engine nor the platform lives in one place in the core library, and is tested on fake time.
- The engine chosen in #8 plugs in without changing that logic.
- Each scenario has one way of being checked: a unit test, or a check by hand on a device.

**Non-Goals:**
- Choosing the engine and the buffer size (#8).
- The player screen (#10).

## Decisions

### Playback logic in the core library
One player component in the core library owns all of this:
- the playback state: stopped, connecting, playing, reconnecting;
- connection rounds, the waits between them, and stall detection;
- restarts on network changes;
- skipped streams;
- track titles;
- the sleep timer.

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

### One 10-second limit for stalls and for attempts
Q29's 10 seconds without data counts a playing connection as stalled. The same limit also fails a connection attempt that hasn't produced audio data. This one rule bounds the time a round spends on a dead primary.

For HLS, the limit is 10 seconds plus the playlist's target duration, since data arrives a segment at a time. The target duration has to reach the player. The seam then reports it with the data events, or the player reads it from the media playlist itself; #8 shows which of the two the engine allows.

Alternative: a separate, shorter connect timeout. Rejected: a slow but working stream would then fail.

### Playlist URLs resolved in the core library, by extension
The player downloads a URL whose path ends in `.pls`, `.m3u` or `.m3u8` with `HttpClient`, with a small size cap, and parses it:
- PLS: the `FileN=` entries, in order of N;
- M3U: the lines that aren't comments.

An `.m3u8` with `#EXT-X-` tags is HLS, and its URL goes to the engine as a stream. Every other URL goes straight to the engine.

Alternative: the engine's own playlist handling. Rejected: the engine isn't chosen yet, and the rounds need the entries, to try them in order.

Alternative: check the content type of every stream URL. Rejected: it costs an extra connection per stream. Feature 12 asks for `.pls` and `.m3u` playlist URLs, and the extension covers those.

### ICY titles: UTF-8, else Latin-1
ICY metadata (`StreamTitle='…';`) declares no text encoding. The player reads the title as UTF-8 when the bytes are valid UTF-8, and as Latin-1 otherwise. Ogg comments are UTF-8 by definition.

### Network changes from MAUI's `Connectivity`
The app forwards MAUI's `Connectivity.ConnectivityChanged` event to the player on both platforms. The player then starts the waits over, with a round at once, as in "Network changes".

Alternative: each platform's own network callbacks. Rejected, unless #8 finds that MAUI's event misses a switch from Wi-Fi to cellular.

### Android
- **Foreground service:** a foreground service of the media-playback type runs while a station plays or reconnects.
- **Media session:** a media session drives the notification, the lock screen and headset and Bluetooth buttons. #8 picks the library for it, since its test app needs one too. It must not be GPL.
- **Locks:** the app holds a partial wake lock and a Wi-Fi lock while playing or reconnecting, so the CPU and Wi-Fi don't sleep with the screen off.
- **Audio focus:** the app requests audio focus when playback starts.
  - A temporary loss, such as a call, stops playback, and the station plays again when focus returns.
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
- the sleep timer.

Scenarios that need a real device are checked by hand on an Android phone and on Windows: background playback, system controls, calls, prompts, another app taking over, output changes, the 5-second targets, and the sleep timer with the screen off. The implementation PR lists each one with its result.

This departs from the `neradio-surgical-change` rule that every scenario is pinned by a test, so the PR calls it out. Alternative: Android device tests on an emulator in CI. Rejected for these scenarios: an emulator doesn't reproduce power saving, calls or real network switches, which is also why #8 uses a real phone.

## Risks / Trade-offs

- [Some phone makers' battery savers kill foreground services despite Android's rules] → #8 runs on the owner's own phone. If it happens there, a later change guides the user to exempt the app.
- [MAUI's connectivity event can fire for a change that doesn't affect the connection in use, such as a cellular network appearing while on Wi-Fi] → The app reconnects needlessly. The buffer usually hides it, so this is accepted.
- [A dead primary that drops packets instead of refusing the connection] → Starting can take 10 seconds before the alternate is tried. Accepted, so that slow but working streams don't fail.
- [Stations that send Cyrillic titles in Windows-1251] → The titles are garbled under the Latin-1 fallback. A later change could pick the fallback from the station's `language`.
- [A playlist URL without a `.pls` or `.m3u` extension] → It goes to the engine as a stream, fails as an unsupported format, and is skipped. Accepted as rare: catalogs usually list resolved stream URLs.
- [The engine can't switch from the old connection's buffer without a gap] → Short drops cause a short silence. If #8 shows this, the app feeds the audio to the output itself, at the cost of more code.
- [A title arrives when its data is downloaded, ahead of the audio by up to the buffer's length] → The shown title changes a few seconds early. Recording (#13, Q26) decides whether to delay it.
- [Reconnecting never gives up, with the wake lock held] → A long outage drains the battery: at most one round every 30 seconds. Accepted, following Q28; Stop and the sleep timer end it.
- [A live stream can't replay what it broadcast during a drop] → After a reconnect, the station goes on from the live broadcast. That follows from live radio.
