# Tasks

#8 chose BASS. Implementation starts once the project skeleton (#9) is on `main`. Groups 1 and 2 go in the core library and its tests. Each scenario they cover gets a unit test that mirrors its WHEN/THEN, running on the fake engine and a fake `TimeProvider`. Groups 3 to 5 are checked by hand, as design.md says, and the PR lists each scenario with its result.

## 1. Player logic

- [x] 1.1 Add the engine seam and a fake engine for the tests, as in design.md. Verify the solution builds for every target.
- [ ] 1.2 Add the connections: an `HttpClient` on a `SocketsHttpHandler` that asks for ICY metadata, takes it out of the stream and passes the rest to the engine, with the HTTP status, the content type and the bytes arriving; and HLS: the variant with the highest bandwidth, each segment once and in order, the AAC taken out of MPEG-TS and packed-audio segments, and the next segment after a reconnect. Verify that tests pin, on a fake HTTP handler with recorded stream data, the ICY metadata taken out of the stream, "Server answers with an error" and "Wi-Fi login page", and every scenario of "No repeated audio after an HLS reconnect".
- [ ] 1.3 Add the player: one station at a time, stopping, connection rounds, the 10-second limit, the waits, and skipped formats. Verify that tests pin every scenario of these requirements, including the ones where something must not happen:
  - "One station at a time" and "Stopping a live stream";
  - "Lost connections" and "Connection rounds";
  - "Waits between rounds" and "Unsupported formats".
- [ ] 1.4 Add network-change restarts: a network change starts the waits over, with a round at once. Verify that tests pin the round logic of "Network changes": a playing connection is dropped, a round under way is abandoned, a wait in progress is skipped, and "First round on the new network fails".
- [ ] 1.5 Add playlist URL resolution for `.pls`, `.m3u` and `.m3u8`, telling HLS apart, with a download, limited to 10 seconds, each time a round reaches the playlist. Verify that tests pin every scenario of "Playlist URLs".
- [ ] 1.6 Add track titles: ICY title parsing with the UTF-8, else Windows-1252 rule, Ogg comment titles, the rules for empty and repeated titles, and each title held until decoding reaches it. Verify that tests pin every scenario of "Track titles", "Repeated titles aren't changes" and "Titles show with their audio".
- [ ] 1.7 Add the kinds of stop: the user's, an interruption, and another stop by the system. When an interruption ends, the station plays again from its primary stream, with the waits started over and the last title kept, unless any stop came in the meantime. Verify that tests pin the player's part of "Phone calls and other interruptions": the station plays again from its primary stream with the waits started over, the same title isn't a change, and nothing plays again after a stop by the user or by the system.

## 2. Sleep timer

- [ ] 2.1 Add the sleep timer to the player: time left, stopping when it runs out, and cancelling by the user's stops only. Verify that tests pin every scenario of "Setting the sleep timer" and "Cancelling the sleep timer" except "Screen off on Android".

## 3. Engine adapter

- [ ] 3.1 Implement the engine seam with BASS, through ManagedBass, with BASSmix and BASSOPUS fetched at build time: a push stream for each connection in one mixer, held paused until about 4 seconds of its audio are buffered, with a 60-second buffer; `BASS_CONFIG_AM_DISABLE` set to 2 on Android; and on Windows, AAC decoded by Windows' own AAC decoder through NAudio.Wasapi and pushed into the mixer. That covers the playing, buffer-ran-out, unsupported-format and Ogg title events; how far decoding has got; the volume; and switching from the old connection's buffer to a new one. Verify by hand, on Android and on Windows, the scenarios of:
  - "Stream formats" and "Plain-http streams";
  - "Server answers with an error" and "Wi-Fi login page" of "Unsupported formats";
  - "Track titles" and "Titles show with their audio", with real ICY and Ogg streams;
  - "HLS segments", "Stalled HLS stream" and "HLS reconnect";
  - "Buffered audio while reconnecting".

## 4. Android

- [ ] 4.1 Add the media-playback foreground service and the media session: the notification, the lock screen, headset and Bluetooth buttons, the wake and Wi-Fi locks, and the permissions. The service stays in the foreground through an interruption, and the media session stays active after a stop. Verify by hand on a real phone the scenarios of "Background playback" and "System media controls", and "Screen off on Android" of the sleep timer.
- [ ] 4.2 Add audio focus and the "audio becoming noisy" broadcast. Verify by hand on a real phone the scenarios of:
  - "Phone calls and other interruptions" and "Short prompts from other apps";
  - "Another app takes over";
  - "Audio output changes";
  - "Phone call" of the sleep timer.
- [ ] 4.3 Forward MAUI's connectivity changes to the player. Verify by hand on a real phone "Wi-Fi to cellular" and "Airplane mode on and off".

## 5. Windows

- [ ] 5.1 Connect the player to the system media transport controls. Verify by hand "Media key on Windows" and "Title in Windows' media controls".
- [ ] 5.2 Follow the default output device, and forward MAUI's connectivity changes to the player. Verify by hand "Minimized on Windows" and "Headphones unplugged on Windows", and that a network change on Windows starts a round at once.

## 6. Verification

- [ ] 6.1 Run the CI entry point from #9: first the tests added here, then the full suite. Verify that the build and the tests pass on both Windows and Android, and that the PR lists every hand-checked scenario with its result.

## Workflow follow-up

- The work can take several implementation PRs. The last one says `Fixes #7` and archives this change, which creates `openspec/specs/playback/spec.md` and `openspec/specs/sleep-timer/spec.md`.
- After archiving, `openspec validate --all` passes.
