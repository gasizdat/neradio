# User Story

As a user, I want a cross-platform application for listening to internet radio stations that runs at least on Windows and Android. Linux is out of scope.

neradio is freeware with no ads, released under the MIT license. No income from it is planned: no donations and no paid tier. Rules for dependencies and licenses are in [AGENTS.md](../../AGENTS.md).

A statement marked *(open: Qn)* depends on a question in [Open and unclear questions](#open-and-unclear-questions).

## Terms

- **Station list**: a file of radio stations. Each station has a name, a logo URL, one or more stream URLs and, in neradio's own lists, a stable station ID. A list from elsewhere may have no IDs.
- **Station catalog**: the predefined station list that the app ships with and keeps up to date (Features 1–2).
- **Group**: a named set of stations and recorded tracks chosen by the user, such as Favorites.
- **Playlist**: a named, ordered list of stations and recorded tracks that the app plays one after another (UC5).
- **Override**: the user's own name or logo for a station or group. It's stored separately from the list the station came from, keyed by the station's or group's identity.

## Main Use Cases

1. The user must be able to load additional radio station lists *(open: Q9)*.

2. The user must be able to export and import radio station lists. A station that already exists is skipped, not imported again *(open: Q12)*.
   - Two entries are the same station when their station IDs match.
   - An entry without an ID matches another when their normalized stream URLs match.

3. The user must be able to create custom radio station groups from different lists. A group can also hold recorded tracks (UC8).
   - 3.1. The default group is **Favorites** *(open: Q18)*.
   - 3.2. The same radio station can belong to multiple user groups.
   - 3.3. When a radio station is selected, all user groups that contain it are displayed, and the user can add it to or remove it from a group from there.
   - 3.4. The user must be able to change the name and logo of any custom or built-in radio station group *(open: Q17)*.

4. The user must be able to change the name and logo of any radio station that they added to a group *(open: Q16)*.
   - The change is stored as an override, so updating the list the station came from doesn't overwrite it.
   - The user can reset a station to its original name and logo.

5. The user must be able to create named playlists containing radio stations and previously recorded tracks. The app plays the items in order and switches automatically *(open: Q20)*:
   - from a station, when the stream reports a new track title, or after a set time;
   - from a station that reports no titles, after the set time only;
   - from a recorded track, when the track ends.

   The original brief switched "after a pause in the broadcast". Stations compress and crossfade their audio, so a gap that the app can detect is rare, and the title change replaces it.

6. The user must be able to view the radio station's track list *(open: Q22, Q23)*.
   - The app builds this history itself from the track titles that the stream reports while the user listens.
   - Where a station publishes its own history (Shoutcast v2, AzuraCast, a station's own feed), the app shows that as well. A stream itself reports only the current title, and there is no standard for history.

7. The user must be able to browse the station's track list and play any track if this is allowed by the station. Whether to keep it is deferred to a later decision: a live stream can't replay earlier tracks, and only some platforms offer on-demand archives *(Q21)*.

8. The user must be able to record favorite tracks and save them to any of their groups, with offline playback support *(open: Q25–Q27)*.
   - "Download" in the original brief means this recording; there's no separate download.
   - Recording saves the raw stream to files, without re-encoding.
   - The recording is split into tracks where the track title changes.

---

# Main Application Features

1. The application must display a predefined list of radio stations: the station catalog *(open: Q15)*.
   - The catalog lives in its own repository with its own data license, so it can be updated and contributed to without an app release.
   - Station logos aren't bundled with the catalog or the app. The app loads them from their URLs at runtime and shows a fallback when a logo is missing or too small.

2. The application must update the catalog from a known URL: a GitHub release asset (`…/releases/latest/download/stations.json`) or a raw file in the catalog repository *(open: Q13, Q14)*.
   - A GitHub Actions artifact can't be used: downloading one requires a signed-in GitHub user, even from a public repository, and artifacts expire.
   - The app downloads the catalog only when it has changed (a conditional request, such as ETag).
   - The user's groups, overrides and playlists survive an update.

3. The application must allow the user to select a background *(open: Q34)*.

4. The application must include multiple tabs/screens *(open: Q8, Q36)*.
   - Screens: Browse/Search, Groups, Playlists, Recordings, Now Playing, Settings (equalizer, background, list sources).
   - Phone layout: bottom tabs and a mini-player. Desktop layout: a side bar and a permanent player bar. Both layouts are designed from the start.

5. The application must aggressively restore lost connections to radio stations.
   - The app keeps retrying, waiting longer between attempts up to a set maximum.
   - A stalled stream (still connected, but no data arriving) counts as a lost connection.
   - When the network changes (Wi-Fi to cellular and back), the app reconnects at once instead of waiting for a timeout.
   - When a station has alternate stream URLs, the app falls back to them.
   - A buffer rides out short drops.
   - Target: a Wi-Fi to cellular switch and an airplane-mode toggle recover within a few seconds, without user action.

6. The application must display complete information about the currently playing station *(open: Q32)*.

7. The application must include a built-in equalizer *(open: Q33)*.
   - 10 bands, applied live while playing.
   - The app processes the audio itself. Android's system equalizer can't be used, because it has a different number of bands on each device (often 5).

8. The user must be able to enable a generated animated color-music visualization that changes in sync with the music *(open: Q35)*.
   - It's driven by frequency data from the app's own audio processing, at 30 or more frames per second.
   - It needs no microphone permission. Android's system visualizer requires one, so it can't be used.

The features below were missing from the original brief.

9. The user must be able to search and filter stations.

10. The user must be able to set a sleep timer that stops playback after a chosen time.

11. Playback must continue with the screen off and the app in the background, controlled from the lock screen, the notification, Bluetooth and headset buttons on Android, and the system media controls on Windows.

12. The application must play:
    - MP3, AAC, Ogg/Opus and HLS streams;
    - stations given as .pls and .m3u playlist URLs;
    - plain-http streams. Many stations are http-only, and Android blocks plain http by default, so the app must allow it explicitly.

---

# Decisions

The original open questions and where they stand:

1. **Programming language: C#.**

2. **Framework: .NET MAUI.** Linux is out of scope, so Avalonia, the alternative that supports it, isn't needed.
   - Electron is ruled out: it doesn't run on Android.
   - A self-hosted web app is ruled out. Many streams send no CORS headers, which leaves the equalizer and visualizer silent. A browser can't read the track titles embedded in a stream, so there would be no titles, history, title-based switching or recording splits. An https page can't play http streams. And Android needs native code for background playback anyway.

3. **Specification: OpenSpec**, set up in `openspec/`. This file stays the brief; the requirements are written as OpenSpec specs in `openspec/specs/`.

4. **Design and HTML concept demo: open** *(open: Q8, Q36)*.

5. **Naming and repository: neradio**, at `github.com/gasizdat/neradio` *(open: Q37–Q39)*.

**Audio engine: BASS**, through ManagedBass, chosen by the engine test (#8). Every audio feature above needs direct access to the stream's bytes and audio samples, so the engine decides more than the UI framework does.

- BASS is built for internet radio, and its free non-commercial license fits an app with no ads and no income. It's closed source, so the app can't go on F-Droid.
- The alternatives were LibVLCSharp, or a native engine per platform (Media3/ExoPlayer on Android, Media Foundation or NAudio on Windows). They weren't tested, since BASS passed.
- Each candidate was to get a 2–3 day test on a real Android device; an emulator doesn't reproduce power saving or real network changes. It passes when it delivers:
  - every stream format in Feature 12;
  - track-title changes as events (UC5, UC6, UC8);
  - 30+ minutes of background playback with working controls (Feature 11);
  - network recovery within a few seconds (Feature 5);
  - a live 10-band equalizer (Feature 7);
  - frequency data at 30+ fps without the microphone permission (Feature 8);
  - raw-stream recording split at title changes (UC8).

  Whatever the engine fails, the app has to build itself.
- BASS passed in an afternoon's session on a Galaxy A73 (Android 16), and on a Windows 11 PC, instead of 2–3 days: see the [PC](https://github.com/gasizdat/neradio/issues/8#issuecomment-6959503878) and [phone](https://github.com/gasizdat/neradio/issues/8#issuecomment-7064295467) results. Three BASS problems need workarounds in the app, listed in [#8's summary](https://github.com/gasizdat/neradio/issues/8#issuecomment-7064789810): its own stall watchdog, an immediate retry on Android, and AAC decoded by Windows' own media stack.
- What the session left open is checked by hand in #7's tasks, on the owner's phone, in place of the days of use: Wi-Fi to cellular (4.4), an hour unplugged with the screen off and a real headset button (4.1), and headphones unplugged on Windows (5.2). Listening for equalizer glitches goes to #14.

---

# Open and unclear questions

Where a question has a proposed default, it's given after it. An answered question keeps its number and is marked Resolved, or Deferred when the decision is postponed, so references to it stay valid.

## Scope and platform

- **Q1. Resolved:** Linux is out of scope, so the framework stays MAUI.
- **Q2.** Is sync between devices (groups, overrides, playlists) in scope? *Default: not in the first version.*
- **Q3. Resolved:** Features 9–12 (search and filter, sleep timer, background playback, stream formats) are in scope.
- **Q4. Resolved:** no income is planned, which keeps the app within BASS's free non-commercial license.
- **Q5. Resolved (#8):** yes. [un4seen's terms](https://www.un4seen.com/bass.html) make BASS free for a non-commercial entity, such as an individual, that makes no money from its product, through sales, advertising or otherwise. They don't mention app stores. BASSmix, BASSHLS and BASSOPUS are free to use with BASS, and BASS_FX may be passed on as long as no fee is charged. BASS_SSL, which Android needs for https, is built on OpenSSL, whose license asks for an attribution notice in the app (#7). Making any money from the app would need a paid BASS license for each platform.
- **Q6.** Where will the app be distributed: Google Play and the Microsoft Store only, or also direct downloads (GitHub Releases, an APK)? F-Droid is out while BASS is the engine.

## Audio engine and UI

- **Q7. Resolved (#8):** BASS, through ManagedBass. See Audio engine in Decisions.
- **Q8.** MAUI with XAML, or MAUI Blazor Hybrid? Blazor Hybrid lets the HTML concept demo become the real UI.

## Station lists and catalog

- **Q9.** Where do additional lists (UC1) come from: a local file, a URL, or both? *Default: both.*
- **Q10. Resolved (#3):** one JSON format, with a format version, for the catalog, import and export. A station has a name, one or more stream URLs (primary first), and optionally an ID, a logo URL, genres, country, language and bitrate. The ID is a UUID; lists from elsewhere may leave it out.
- **Q11. Resolved (#3):** http vs. https, the host's letter case, an explicit default port and a trailing slash don't count. Any other difference, such as a mirror's host or another access token, makes a different station.
- **Q12.** Does an export carry only stations, or also the user's groups, overrides and playlists?
- **Q13.** What happens to a station the user has grouped when it disappears from the catalog? *Default: it stays in the user's groups, marked as no longer in the catalog.*
- **Q14.** How often does the app check for catalog updates? *Default: at startup, at most once a day.*
- **Q15.** What are the catalog repository's name, its initial source of stations, and its data license? CC0 fits a list you compile yourself; a list taken from a source such as Radio Browser keeps that source's terms.

## Groups, overrides and playlists

- **Q16.** Does renaming a station apply everywhere, or only in the group where the user renamed it? *Default: everywhere.*
- **Q17.** Which groups are built in besides Favorites (3.4)? Are catalog categories, such as genre or country, groups?
- **Q18.** Can Favorites be deleted? *Default: no; it can only be renamed and given a logo.*
- **Q19. Resolved:** groups hold both stations and recorded tracks.
- **Q20.** How does a playlist switch? Does it switch on every title change or after several tracks, is the time limit set per playlist or per item, and does it loop at the end? *Default: on every title change, one time limit per playlist, loops.*

## Track lists and recording

- **Q21. Deferred:** whether to keep or drop UC7 (playing earlier tracks) is decided later.
- **Q22.** Which station history sources does the first version support: Shoutcast v2, AzuraCast, station feeds, or only the app's own history? *Default: only the app's own history.*
- **Q23.** How much of its own history does the app keep per station, and does it survive a restart?
- **Q24. Resolved:** "download" in UC8 means recording the live stream.
- **Q25.** Can the app record a station other than the one playing, or record on a schedule? *Default: only the station playing.*
- **Q26.** A title change doesn't fall exactly on the track boundary, so a recorded track can start or end a few seconds off. Is that acceptable, or does the user need to trim tracks?
- **Q27.** Where are recordings stored: in the app's private storage, or in a folder the user can see, such as Music?

## Reconnecting (Feature 5)

- **Q28. Resolved (#7):** retry at once, then after waits of 1, 2, 4, 8 and 16 seconds, then every 30 seconds, until playback stops. The waits start over when the user plays a station, when the network changes, and once audio has played for 60 seconds.
- **Q29. Resolved (#7):** 10 seconds without data. The same limit fails a connection attempt that gets no audio.
- **Q30. Resolved (#8):** playback starts once about 4 seconds of audio are buffered, and the buffer holds up to 60 seconds. On connecting, servers send 11–35 seconds of audio at once, so a buffer that size fills at once without delaying the start, and then rides out a drop of that length. In the test, audio started 0.6–3.7 s after Play and 3.6 s after the network came back, inside the 5-second targets. A title can then arrive up to 35 s before its audio plays, so the shown title has to wait for the audio (#7).
- **Q31. Resolved (#7):** on Android, the app stops for a call or an alarm and plays again after it, lowers its volume for a short prompt, and stops without starting again when another app starts playing music. On Windows, the system handles calls.
- **Q41.** Should the app, as an option, keep a lower-bitrate stream of the station downloading alongside the one playing, and switch to it when the main stream's buffer runs out? *Default: not in the first version. Switching to a lower-bitrate stream after the main one stalls is considered first: it uses no extra data, but leaves a short gap.*
  - It helps when the network gets too slow for the main stream, or when only the main stream's server fails.
  - It doesn't help when the phone loses its network, because both streams stop at the same moment of the broadcast. Keeping the spare on cellular while the main stream uses Wi-Fi would cover a lost Wi-Fi connection, if Android and the engine allow it.
  - It uses extra data all the time, about 14 MB an hour at 32 kbps. On a slow network, the spare also takes bandwidth from the main stream.
  - A station's streams come from separate encoders and may be out of sync, so the app has to line them up when it switches, and again when it switches back.
  - The app has to know which stream is the same programme at a lower bitrate. An alternate stream isn't always one: it can be a network's shared fallback.

## Player and appearance

- **Q32.** What does "complete information" (Feature 6) include? *Default: name, logo, current track title, stream format and bitrate, genre and country from the catalog, and the groups that contain the station.*
- **Q33.** Is the equalizer global or per station, and does it have presets? *Default: global, with presets.*
- **Q34.** What can a background (Feature 3) be: built-in presets, the user's own images, or both? Is there one for the whole app, or one per screen or station? *Default: both kinds, one for the whole app.*
- **Q35.** Does the visualization have one style or several? *Default: a few styles. It runs only while visible, to save battery.*

## Design

- **Q36.** The HTML concept demo isn't built yet. It should settle:
  - how the station view shows the groups that contain the station (3.3), with quick add and remove;
  - how renamed stations are shown, and how they're reset;
  - the fallback for missing or tiny station logos;
  - how stations and recorded tracks are mixed in the playlist editor.

## Naming, release and builds

- **Q37.** Neradio or NeRadio? The spelling appears in namespaces and the store listing.
- **Q38.** Which Android application ID? It can't change after the first Google Play upload. *Default: `io.github.gasizdat.neradio`.*
- **Q39.** Is the name free on Google Play, in the Microsoft Store and as a domain (neradio.app or .com)? Check before the first store upload.
- **Q40.** Should CI build the app for both Windows and Android from the first code change? *Default: yes.*
