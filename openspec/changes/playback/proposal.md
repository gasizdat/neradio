# Proposal

## Why

Playing a station is the app's core, and the audio engine is chosen by how well it does that on a real Android phone (#8). The engine test needs a fixed list of what "plays well" means, and today the brief gives only targets such as "recover within a few seconds". This change turns Features 5, 10, 11 and 12 into scenarios that the engine test checks one by one, and that the app's implementation is later held to.

It settles Q28 (retries), Q29 (stalled streams) and Q31 (calls and other apps' sound). Q30 (buffer size) stays open: the engine test sets it.

## What Changes

- **Stream formats (Feature 12):** MP3, AAC, Ogg Vorbis, Opus and HLS; stations given as `.pls`, `.m3u` and `.m3u8` playlist URLs; and plain-http streams on both platforms.
- **One station at a time, and one meaning of "stop".** A live stream can't be paused and resumed where it left off. Stopping disconnects, and Play starts the live broadcast again. The pause button in the system controls stops too.
- **Reconnecting (Feature 5), following Q28's and Q29's defaults:**
  - A connection error, the stream ending, or 10 seconds without data counts as a lost connection (Q29).
  - The app reconnects in rounds. Each round tries the station's stream URLs in turn, so alternate streams are used when the primary fails.
  - The first round starts at once. Later rounds wait longer and longer, up to 30 seconds. The app never gives up until the user stops playback (Q28).
  - A network change makes the app reconnect at once instead of waiting.
  - The app keeps playing the audio it has buffered while it reconnects.
- **Background playback and system controls (Feature 11):** the station keeps playing with the screen off and the app in the background. It's controlled from the lock screen, the notification, and Bluetooth and headset buttons on Android, and from the system media controls on Windows.
- **Calls and other apps' sound, following Q31's default:** the app stops for a phone call and plays again after it, and lowers its volume while another app plays a short prompt, such as a navigation instruction. This applies on Android; on Windows the system handles it.
- **Sleep timer (Feature 10):** playback stops after a time the user chooses.

Choices that go beyond the brief and the proposed defaults, for review:

- **Track titles are in this change.** The stream's current title is shown, and each new title counts as a title change. Playlists (UC5), track history (UC6) and recording (UC8) all act on title changes, and the engine test checks them, but no issue covered them.
  - Only titles carried in the stream itself are read: ICY metadata in MP3 and AAC streams, and comments in Ogg streams. HLS streams carry titles in no single standard way, so an HLS station counts as one that reports no titles, which UC5 already allows for.
  - A title that repeats the current one isn't a change, even right after a reconnect. Otherwise every reconnect would split a recording or add a duplicate to the history.
- **How the formats are read.** "Ogg/Opus" in Feature 12 is read as both Ogg Vorbis and Opus. AAC includes HE-AAC, which many low-bitrate stations use. HLS means streams with AAC audio.
- **"Within a few seconds" becomes 5 seconds.** After switching from Wi-Fi to cellular, any silence lasts at most 5 seconds. After airplane mode is turned off, audio returns within 5 seconds of the phone reconnecting to a network.
- **The details of retrying:**
  - Pressing Play starts with the primary stream. Every later round starts with the stream that played last, so a station that has fallen back to an alternate doesn't wait on a dead primary first.
  - A playlist URL is downloaded again each time a round reaches it, and a download that fails counts as a failed stream. The stream that played last can be one that a playlist listed.
  - The waits are 1, 2, 4, 8 and 16 seconds, then 30 seconds from then on.
  - The waits start over when the user plays a station, when the network changes, and once audio has played for 60 seconds. They don't start over on every reconnect, so a stream that keeps dropping right after connecting is retried less and less often, instead of every second.
  - A network change restarts the connection even when the old one still seems alive. On a switch from Wi-Fi to cellular, the old connection would otherwise hang until it timed out.
  - HLS streams get their data a segment at a time, so for them the 10 seconds without data count on top of the playlist's target duration.
- **A format the app can't play ends the retries.** Retrying a stream in an unsupported format can't help. Such a stream is skipped, and if no stream of the station can be played, playback stops and the app says why. This is the one exception to "never gives up". Only audio the app can't decode counts: an HTTP error, or a web page such as a Wi-Fi login page, is a failed connection, and the retries go on.
- **Losing audio focus and the headphones, beyond Q31:**
  - Anything else that takes the audio for a while, such as an alarm or a voice assistant, is handled like a call. After it, the station plays again as if the user had pressed Play: from the primary stream, with the waits started over.
  - When another app starts playing music, the app stops and doesn't start again on its own.
  - When headphones are unplugged or a Bluetooth audio device disconnects, playback stops on Android rather than switching to the speaker. On Windows it carries on with the new default output device, as Windows apps usually do.
- **Only the user cancels the sleep timer,** by cancelling it or by stopping playback. Stops the user didn't make keep it running, such as a stop for a call, another app taking over, or unplugged headphones. Switching to another station keeps it too.

Out of scope:
- next and previous in the system controls (screens, #10, and playlists, #11);
- the equalizer and visualizer (#14);
- recording (#13);
- what the player screen shows (#10).

## Capabilities

### New Capabilities
- `playback`: playing a station. That covers stream formats and playlist URLs, track titles, reconnecting, background playback, system media controls, and reacting to calls, other apps' sound and output changes.
- `sleep-timer`: stopping playback after a chosen time.

### Modified Capabilities

None.

## Impact

- **The engine test (#8):** these scenarios are its pass list, together with the equalizer, visualizer and recording criteria in Decisions. Q30's buffer size must let the 5-second targets hold.
- **Code, once #8 and #9 are done:**
  - Playback logic in the core library: retries, stall detection, playlist URLs, track titles and the sleep timer.
  - Platform code in the app: background playback, system media controls and audio focus on Android, and system media controls on Windows.
- **Android manifest:** new permissions for a media-playback foreground service and for notifications, and a setting that allows plain-http traffic.
- `doc/entrypoint/UserStory.md`: Q28, Q29 and Q31 are marked Resolved.
