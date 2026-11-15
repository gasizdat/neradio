# Tasks

Implementation starts once the engine is chosen (#8) and the project skeleton (#9) is on `main`. Groups 1 and 2 go in the core library and its tests. Each scenario they cover gets a unit test that mirrors its WHEN/THEN, running on the fake engine and a fake `TimeProvider`. Groups 3 to 5 are checked by hand, as design.md says, and the PR lists each scenario with its result.

## 1. Player logic

- [ ] 1.1 Add the engine seam and a fake engine for the tests, as in design.md. Verify the solution builds for every target.
- [ ] 1.2 Add the player: one station at a time, stopping, connection rounds, the 10-second limit, the waits, and skipped formats. Verify that tests pin every scenario of these requirements, including the ones where something must not happen:
  - "One station at a time" and "Stopping a live stream";
  - "Lost connections" and "Connection rounds";
  - "Waits between rounds" and "Unsupported formats".
- [ ] 1.3 Add network-change restarts: a network change starts the next round at once, and the later waits keep their sequence. Verify that tests pin the round logic of "Network changes": a playing connection is dropped, a round under way is abandoned, and a wait in progress is skipped.
- [ ] 1.4 Add playlist URL resolution for `.pls`, `.m3u` and `.m3u8`, telling HLS apart. Verify that tests pin every scenario of "Playlist URLs".
- [ ] 1.5 Add track titles: ICY title parsing with the UTF-8, else Latin-1 rule, Ogg comment titles, and the check for repeated titles. Verify that tests pin every scenario of "Track titles" and "Repeated titles aren't changes".

## 2. Sleep timer

- [ ] 2.1 Add the sleep timer to the player: time left, stopping when it runs out, and cancelling. Verify that tests pin every scenario of "Setting the sleep timer" and "Cancelling the sleep timer" except "Screen off on Android".

## 3. Engine adapter

- [ ] 3.1 Implement the engine seam with the engine chosen in #8. That covers opening streams, the data, end, failure and title events, the volume, and switching from the old connection's buffer to a new one. Verify by hand, on Android and on Windows, the scenarios of:
  - "Stream formats" and "Plain-http streams";
  - "Buffered audio while reconnecting".

## 4. Android

- [ ] 4.1 Add the media-playback foreground service and the media session: the notification, the lock screen, headset and Bluetooth buttons, the wake and Wi-Fi locks, and the permissions. Verify by hand on a real phone the scenarios of "Background playback" and "System media controls", and "Screen off on Android" of the sleep timer.
- [ ] 4.2 Add audio focus and the "audio becoming noisy" broadcast. Verify by hand on a real phone the scenarios of:
  - "Phone calls" and "Short prompts from other apps";
  - "Another app takes over";
  - "Audio output changes";
  - "Phone call" of the sleep timer.
- [ ] 4.3 Allow cleartext traffic in the manifest. Verify by hand "http stream on Android".
- [ ] 4.4 Forward MAUI's connectivity changes to the player. Verify by hand on a real phone "Wi-Fi to cellular" and "Airplane mode on and off".

## 5. Windows

- [ ] 5.1 Connect the player to the system media transport controls. Verify by hand "Media key on Windows" and "Title in Windows' media controls".
- [ ] 5.2 Follow the default output device, and forward MAUI's connectivity changes to the player. Verify by hand "Minimized on Windows" and "Headphones unplugged on Windows", and that a network change on Windows starts a round at once.

## 6. Verification

- [ ] 6.1 Run the CI entry point from #9: first the tests added here, then the full suite. Verify that the build and the tests pass on both Windows and Android, and that the PR lists every hand-checked scenario with its result.

## Workflow follow-up

- The work can take several implementation PRs. The last one says `Fixes #7` and archives this change, which creates `openspec/specs/playback/spec.md` and `openspec/specs/sleep-timer/spec.md`.
- After archiving, `openspec validate --all` passes.
