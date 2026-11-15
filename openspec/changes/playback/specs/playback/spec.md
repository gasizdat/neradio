# Spec Delta

## Purpose

Defines how the app plays a radio station: the stream formats it plays, the track titles it reads, how it keeps a station playing through lost connections, and how it plays in the background under the system's controls.

## ADDED Requirements

### Requirement: One station at a time
The app SHALL play one station at a time. Playing a station SHALL stop the one that was playing.

#### Scenario: Playing another station
- **WHEN** a station is playing and the user plays another one
- **THEN** the first station stops and the second one plays

### Requirement: Stopping a live stream
Stopping SHALL disconnect from the stream and end any reconnecting. The station SHALL stay selected, and Play SHALL connect to its live broadcast again, not resume from where it stopped. The pause button of the system media controls SHALL stop playback.

#### Scenario: Stop and play again
- **WHEN** the user stops a station, waits a minute, and presses Play
- **THEN** the same station plays from its live broadcast, not from where it stopped

#### Scenario: Stopping while reconnecting
- **WHEN** the user stops playback while the app is reconnecting
- **THEN** the app makes no further connection attempts

### Requirement: Stream formats
The app SHALL play streams in these formats: MP3; AAC, including HE-AAC; Ogg Vorbis; Opus in Ogg; and HLS with AAC audio.

#### Scenario: MP3 stream
- **WHEN** the user plays a station whose stream is MP3
- **THEN** it plays

#### Scenario: AAC stream
- **WHEN** the user plays a station whose stream is AAC-LC
- **THEN** it plays

#### Scenario: HE-AAC stream
- **WHEN** the user plays a station whose stream is HE-AAC
- **THEN** it plays

#### Scenario: Ogg Vorbis stream
- **WHEN** the user plays a station whose stream is Ogg Vorbis
- **THEN** it plays

#### Scenario: Opus stream
- **WHEN** the user plays a station whose stream is Opus in Ogg
- **THEN** it plays

#### Scenario: HLS stream
- **WHEN** the user plays a station whose stream URL is an HLS playlist with AAC audio
- **THEN** it plays

### Requirement: Plain-http streams
The app SHALL play stream URLs and playlist URLs that use plain http, on Android as well as on Windows.

#### Scenario: http stream on Android
- **WHEN** the user plays, on Android, a station whose only stream URL starts with `http://`
- **THEN** it plays

### Requirement: Playlist URLs
A stream URL whose path ends in `.pls`, `.m3u` or `.m3u8` SHALL be read as a playlist, unless it's an HLS playlist. The app SHALL use the stream URLs the playlist lists, in their order, in place of the playlist URL. A playlist that lists no usable stream URL SHALL count as a stream that failed to connect.

#### Scenario: PLS playlist
- **WHEN** a station's stream URL is a `.pls` file listing two stream URLs, and the first doesn't connect
- **THEN** the second one plays

#### Scenario: M3U playlist
- **WHEN** a station's stream URL is an `.m3u` file listing a stream URL
- **THEN** that stream plays

#### Scenario: HLS playlist isn't a list of streams
- **WHEN** a station's stream URL ends in `.m3u8` and is an HLS playlist
- **THEN** the app plays it as an HLS stream

#### Scenario: Playlist without usable streams
- **WHEN** a station's only stream URL is a playlist that lists no usable stream URL
- **THEN** the app treats it as a stream that failed to connect and keeps retrying

### Requirement: Unsupported formats
A stream whose server sends audio in a format the app can't play SHALL be skipped until playback stops. An HTTP error status, or a response that isn't audio, such as a web page, SHALL count as a failed connection instead, and SHALL NOT make the stream skipped. When every stream of the station has been skipped, playback SHALL stop, and the app SHALL tell the user that the station's format isn't supported.

#### Scenario: Primary in an unsupported format
- **WHEN** a station's primary stream is in a format the app can't play, and its alternate is MP3
- **THEN** the alternate plays

#### Scenario: No playable stream
- **WHEN** every stream of a station is in a format the app can't play
- **THEN** playback stops, the app tells the user the format isn't supported, and it doesn't retry

#### Scenario: Server answers with an error
- **WHEN** a station's only stream answers with an HTTP error, such as 404 while the station's server restarts
- **THEN** the app counts it as a failed connection and keeps retrying, and the station plays once the server sends audio again

#### Scenario: Wi-Fi login page
- **WHEN** the phone is on a Wi-Fi network that answers every request with its login page
- **THEN** the app counts each attempt as a failed connection and keeps retrying, and the station plays after the user logs in

### Requirement: Track titles
While a station plays, the app SHALL show the track title the stream reports: ICY metadata in MP3 and AAC streams, and comments in Ogg streams. Each title that differs from the current one SHALL count as a title change. A title that's empty after trimming SHALL count as no title.

#### Scenario: New title
- **WHEN** an MP3 stream reports the title `Artist A - Song 1`, then `Artist B - Song 2`
- **THEN** the app shows `Artist B - Song 2`, and that counts as one title change

#### Scenario: Title in an Ogg stream
- **WHEN** an Ogg Vorbis stream starts a new track whose comments carry a new title
- **THEN** the app shows the new title, and that counts as a title change

#### Scenario: Empty title
- **WHEN** a stream reports an empty title
- **THEN** the app shows no title

#### Scenario: Station without titles
- **WHEN** a stream never reports a title
- **THEN** the app shows no title and reports no title changes

### Requirement: Repeated titles aren't changes
A title equal to the current one SHALL NOT count as a title change, including the first title the stream reports after a reconnect.

#### Scenario: Stream repeats its title
- **WHEN** a stream reports the same title several times in a row
- **THEN** that counts as one title change, not several

#### Scenario: Same title after a reconnect
- **WHEN** the app reconnects to a station, and the stream reports the title it reported before the connection was lost
- **THEN** that doesn't count as a title change

### Requirement: Lost connections
The app SHALL count a connection as lost when it fails, when the stream ends, or when no data has arrived for 10 seconds. An HLS stream receives its data a segment at a time, so its limit SHALL be 10 seconds plus its playlist's target duration. A connection attempt that gets no audio data within 10 seconds SHALL count as failed.

#### Scenario: Server closes the connection
- **WHEN** the server closes the connection while a station plays
- **THEN** the app counts the connection as lost and reconnects

#### Scenario: Stalled stream
- **WHEN** the connection stays open but no data arrives for 10 seconds
- **THEN** the app counts the connection as lost and reconnects

#### Scenario: Short pause in the data
- **WHEN** no data arrives for 9 seconds and then data arrives again
- **THEN** the app keeps the connection and doesn't reconnect

#### Scenario: HLS segments
- **WHEN** an HLS stream's playlist has a target duration of 10 seconds, and a new segment arrives every 10 seconds
- **THEN** the app keeps the connection and doesn't reconnect

#### Scenario: Stalled HLS stream
- **WHEN** an HLS stream's playlist has a target duration of 10 seconds, and no new segment arrives for 20 seconds
- **THEN** the app counts the connection as lost and reconnects

### Requirement: Connection rounds
The app SHALL connect in rounds. A round tries each stream URL of the station that hasn't been skipped once, without waiting between them, and ends when a stream plays or every stream has failed. When the user plays a station, the round starts with the primary stream. Every later round starts with the stream that played last. The others follow in the station's order. While reconnecting, the app SHALL show that it's reconnecting.

#### Scenario: Dead primary when starting
- **WHEN** the user plays a station whose primary stream doesn't connect and whose alternate does
- **THEN** the alternate plays, without a wait between the two attempts

#### Scenario: Lost connection on an alternate
- **WHEN** a station plays from its alternate stream and that connection is lost
- **THEN** the next round tries the alternate first, then the primary

#### Scenario: Reconnecting is shown
- **WHEN** the app is reconnecting
- **THEN** it shows that it's reconnecting, and the station stays the one selected

### Requirement: Waits between rounds
The first round SHALL start at once. Each later round SHALL start after a wait of 1, 2, 4, 8 and 16 seconds, in that order, then 30 seconds for every round after that. A lost connection SHALL NOT restart this sequence. It starts over only when the user plays a station, when the network changes, or once audio has played for 60 seconds. The app SHALL keep reconnecting until playback stops.

#### Scenario: Every stream keeps failing
- **WHEN** a connection is lost and every round fails
- **THEN** the rounds start at once, then after waits of 1, 2, 4, 8, 16, 30, 30 seconds, and so on

#### Scenario: Long outage
- **WHEN** every round has failed for an hour
- **THEN** the app is still starting a round every 30 seconds

#### Scenario: Stream drops soon after reconnecting
- **WHEN** the second round reconnects, and the connection is lost again after 20 seconds of audio
- **THEN** the next round starts after a wait of 2 seconds

#### Scenario: Stream drops after playing a while
- **WHEN** a station has played for 60 seconds since its last reconnect, and the connection is lost
- **THEN** the first round starts at once

### Requirement: Network changes
While a station plays or reconnects, and the device switches to another network or connects to one after having none, the app SHALL start the sequence in "Waits between rounds" over, with its first round at once. It SHALL drop the old connection, even if audio still arrives on it; abandon a round under way; and skip any wait in progress.

#### Scenario: Wi-Fi to cellular
- **WHEN** a station plays over Wi-Fi on a phone, and the phone switches to cellular
- **THEN** the station keeps playing without the user doing anything, and any silence lasts at most 5 seconds

#### Scenario: Airplane mode on and off
- **WHEN** a station plays, airplane mode is turned on for a minute and then off
- **THEN** audio returns within 5 seconds of the phone reconnecting to a network, without the user doing anything

#### Scenario: First round on the new network fails
- **WHEN** the waits have reached 30 seconds, the phone connects to a network, and the round that starts at once fails
- **THEN** the next round starts after a wait of 1 second

### Requirement: Buffered audio while reconnecting
While it reconnects, the app SHALL keep playing the audio it has already buffered. When it reconnects before that audio runs out, the station SHALL play on without silence.

#### Scenario: Short drop
- **WHEN** the server closes the connection, and the app reconnects before the buffered audio runs out
- **THEN** the station plays on without silence

### Requirement: Background playback
A station SHALL keep playing on Android with the screen off or another app in front, and on Windows with the app minimized or behind other windows.

#### Scenario: Screen off on Android
- **WHEN** the user plays a station on an Android phone that's not charging, turns the screen off and leaves the phone for 30 minutes
- **THEN** audio plays the whole time, and the lock-screen controls still work at the end

#### Scenario: Another app in front on Android
- **WHEN** a station plays and the user switches to another app
- **THEN** the station keeps playing

#### Scenario: Minimized on Windows
- **WHEN** a station plays and the user minimizes the app on Windows
- **THEN** the station keeps playing

### Requirement: System media controls
The system media controls SHALL show the station's name and logo and the current track title, and SHALL let the user play and stop the station. On Android, these are the media notification, the lock screen, and Bluetooth and wired headset buttons. On Windows, these are the system media controls, which include the keyboard's media keys.

#### Scenario: Title on the lock screen
- **WHEN** the track title changes while an Android phone is locked
- **THEN** the lock screen shows the new title

#### Scenario: Headset button
- **WHEN** a station plays on Android and the user presses the play/pause button of a Bluetooth or wired headset
- **THEN** playback stops, and pressing it again plays the station

#### Scenario: Media key on Windows
- **WHEN** a station plays on Windows and the user presses the keyboard's play/pause media key
- **THEN** playback stops, and pressing it again plays the station

#### Scenario: Title in Windows' media controls
- **WHEN** the track title changes on Windows
- **THEN** Windows' media controls show the station's name and the new title

### Requirement: Phone calls
On Android, the app SHALL stop for a phone call and play the station again when the call ends. On Windows nothing is required: the system handles calls.

#### Scenario: Incoming call
- **WHEN** a phone call starts while a station plays on Android
- **THEN** playback stops, and the station plays again when the call ends

### Requirement: Short prompts from other apps
On Android, while another app plays a short prompt, such as a navigation instruction, the app SHALL lower its volume, and restore it after the prompt. On Windows nothing is required.

#### Scenario: Navigation prompt
- **WHEN** a navigation app speaks an instruction while a station plays on Android
- **THEN** the station's volume drops during the instruction and comes back after it

### Requirement: Another app takes over
On Android, when another app starts playing audio for an indefinite time, such as music, the app SHALL stop and SHALL NOT start again on its own. On Windows nothing is required.

#### Scenario: Music app starts
- **WHEN** a station plays on Android and the user starts music in another app
- **THEN** the station stops, and it doesn't start again when the music ends

### Requirement: Audio output changes
On Android, when wired headphones are unplugged or a Bluetooth audio device disconnects, the app SHALL stop playback. On Windows, when the output device in use is removed, playback SHALL carry on through the new default output device.

#### Scenario: Headphones unplugged on Android
- **WHEN** a station plays through headphones on Android and the headphones are unplugged
- **THEN** playback stops, and nothing plays from the phone's speaker

#### Scenario: Headphones unplugged on Windows
- **WHEN** a station plays through USB or Bluetooth headphones on Windows and they're disconnected
- **THEN** the station keeps playing through the new default output device
