# User Story

As a user, I want to create a cross-platform application for listening to internet radio stations that can run at least on Windows and Android.

## Main Use Cases

1. The user must be able to load additional radio station lists.

2. The user must be able to export and import radio station lists. If radio stations from a new list already exist, they are ignored and are not imported again.

3. The user must be able to create custom radio station groups from different lists.
   - 3.1. The default group is **Favorites**.
   - 3.2. The same radio station can belong to multiple user groups.
   - 3.3. When a radio station is selected, all user groups that contain it are displayed.
   - 3.4. The user must be able to change the name and logo of any custom or built-in radio station group.

4. The user must be able to change the name and logo of any radio station that they added to a group.

5. The user must be able to create named playlists containing radio stations and previously downloaded/recorded tracks. In this mode, the application automatically switches from one radio station to another after a pause in the broadcast or after a defined period of time.

6. The user must be able to view the radio station's track list if the station provides it.

7. The user must be able to browse the station's track list and play any track if this is allowed by the station.

8. The user must be able to download/record favorite tracks and save them to any of their groups, with offline playback support.

---

# Main Application Features

1. The application must display a predefined list of radio stations.

2. The application must update the predefined radio station list from a known URL source (for example, a GitHub artifact).

3. The application must allow the user to select a background.

4. The application must include multiple tabs/screens.

5. The application must aggressively restore lost connections to radio stations.

6. The application must display complete information about the currently playing station.

7. The application must include a built-in equalizer.

8. The user must be able to enable a generated animated color-music visualization that changes in sync with the music.

---

# Open Questions

1. Programming language (C# or TypeScript).
2. Framework selection (MAUI, Electron, or self-hosted Web App).
3. Detailed specification and OpenSpec evaluation.
4. Application design and HTML concept demo.
5. Project naming and GitHub repository registration.
