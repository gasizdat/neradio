# neradio: system overview

neradio is a free, ad-free internet radio app for Windows and Android. It plays live streams in
the background, keeps them playing through lost connections, and organizes stations into groups
and playlists. The brief is [doc/entrypoint/UserStory.md](../entrypoint/UserStory.md); the
requirements are OpenSpec specs in `openspec/`.

This is the entry point into `doc/dev`. It answers "what is this and how is it shaped". It names no
class: once code exists, the solution file is the live list of projects, and
[principles/](principles/) holds the rules that apply across them.

## Who this is for

A developer or AI agent about to make a change, who needs to find the right place for it without
reading the whole codebase first.

## The shape of the system

**One process, many small services.** Each piece of behavior is a small service with one job,
behind an interface, and the DI container composes them. Services talk through those interfaces,
never through a concrete class they create themselves. See
[principles/dependency-injection.md](principles/dependency-injection.md).

**The app makes every connection; BASS only decodes.** The core downloads each stream itself,
HLS included, and passes the bytes to the audio engine. BASS decodes and mixes them, and on Windows
Windows' own decoder handles AAC. The reasons are in the playback design,
`openspec/changes/playback/design.md`.

**The UI is Blazor Hybrid** (Q8): Razor components with the concept demo's HTML and CSS, hosted in
a MAUI `BlazorWebView`. Audio, the media session and the background service are native code, so
the WebView never carries playback.

## The layers

From the bottom up. Dependencies only point down this list, never up.

- **`Neradio.Core`**, plain `net10.0`: the player logic, the connections (HTTP, ICY titles, HLS),
  station lists, track titles and the sleep timer, with the interfaces the engine and the platform
  implement. No MAUI, no Blazor, no BASS, no platform API. That's what lets its tests run anywhere
  `dotnet test` runs.
- **`Neradio.UI`**, a Razor class library on `net10.0`: the screens as Razor components, and the
  concept demo's CSS. It knows Blazor and `Neradio.Core`, not MAUI, so its components are tested
  without a device.
- **`Neradio.Engine.Bass`**, for `net10.0-android` and `net10.0-windows`: implements the core's
  engine seam with BASS, and on Windows decodes AAC through Windows' own decoder. Platform APIs are
  fine here; MAUI isn't. BASS is fetched at build time and never committed.
- **`Neradio.App`**, MAUI Blazor Hybrid for Android and Windows: the host. It registers every
  service (the composition root), and holds what needs MAUI or the platform's UI: the foreground
  service, the media session, audio focus, Windows' media controls and network changes, each
  behind an interface the core defines.

The tests mirror the layers: `Neradio.Core.Tests`, `Neradio.UI.Tests` and, on Windows,
`Neradio.Engine.Bass.Tests`. See
[principles/testing-strategy.md](principles/testing-strategy.md).

Keeping `Neradio.Core` and `Neradio.UI` free of MAUI is a maintained boundary, not an accident: it
keeps the logic and the screens testable without a device, and keeps the engine replaceable.

## Builds

Both platforms build from the first code change (Q40). CI builds Android and Windows and runs the
tests on every PR. The commands live with the code, in the solution and its scripts, not in this
document.

## Where to go next

- **A rule that applies across the code** → [principles/](principles/).
- **What the app must do** → `openspec/specs/`, and `openspec/changes/` for what's being built.
- **How to make and verify a change** (branches, commits, the surgical-change discipline) →
  `AGENTS.md` and `.agents/skills/`, which this document set doesn't repeat.
