# XAML or Blazor Hybrid (Q8): measured

Two bare MAUI apps draw the same screen: the concept demo's Browse page in its phone layout, with
search, the genre chips, 100 station rows with initials tiles, the mini-player and five tabs.

- **`UiXaml`:** Shell with a tab bar, the list in a `CollectionView`.
- **`UiBlazor`:** a `BlazorWebView` with one Razor component, styled by the demo's own `concept.css`.

`Shared/` holds the stations and the startup mark both apps write. Release builds, .NET 10, MAUI
10.0.20, measured on 22 Nov 2026. Not for merging: like the engine test, it stays on its branch.

## Galaxy A73, Android 16

| | XAML | Blazor Hybrid |
|---|---|---|
| Cold start, first frame (`am start -W`, 10 runs) | 1.21 s | 0.86 s, an empty WebView |
| Cold start, list on screen (from the process start) | about 1.2 s, in the first frame | 1.17 s, painted |
| Memory once settled (PSS) | **142 MB** | **224 MB**: 172 MB app + 52 MB WebView renderer |
| Scrolling, 10 flings (`gfxinfo`) | 2.6% janky; 90th/99th percentile 9/10 ms | 2.8% janky; 11/15 ms |
| Resume from the background (5 runs) | 56 ms | 54 ms |
| APK (arm64) | 17.7 MB | 17.9 MB |

The XAML app's mark fires when the first row is attached, just before its first frame, so its list
shows with that frame. The Blazor app's mark fires two animation frames after its first render.

## Windows 11 26H2

| | XAML | Blazor Hybrid |
|---|---|---|
| List on screen (from the process start, 5 runs) | 1.62 s | 1.49 s |
| Memory once settled (private bytes, whole process tree) | **105 MB**, 1 process | **240 MB**, the app and 6 WebView2 processes |

## How to run it again

- Android: build with `dotnet build <app>.csproj -f net10.0-android -c Release`, install the APK. The
  startup mark is in logcat under the tag `UIMARK`.
- Windows: build with `-f net10.0-windows10.0.19041.0 -c Release` and start the exe; the mark goes to
  `marks.txt` next to it.
