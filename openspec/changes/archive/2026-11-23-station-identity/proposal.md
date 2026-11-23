# Proposal

## Why

Several parts of the app need to know whether two entries are the same station:
- skipping stations that already exist on import (UC2);
- overrides that survive list updates (UC4);
- groups (UC3);
- catalog updates (Features 1–2);
- export.

Each of these needs one station list format and one rule for when two entries are the same station. Without them, every later spec would invent its own. This change settles both: Q10 (the format) and Q11 (which URL differences still count as the same station).

## What Changes

- One station list format, JSON, for the catalog, for lists the user loads, and for exports. This follows Q10's default.
  - The document carries a format version.
  - Each station has an ID, a name, an optional logo URL, and one or more stream URLs, primary first. Genres, country, language and bitrate are optional. The ID is optional in lists from elsewhere.
  - A reader ignores fields it doesn't know. Adding a field doesn't change the version, so older app versions keep reading newer catalogs.
  - An entry without a name or without a usable stream URL is skipped, and the rest of the list loads. A list in an unsupported version is rejected as a whole.
- A normalized form of stream URLs, following Q11's default.
  - It ignores http vs. https, the letter case of the host, an explicit default port and a trailing slash.
  - Everything else must match exactly, so a mirror on another host or a different access token makes a different station.
- Rules for when two entries are the same station:
  - When both have IDs, the IDs decide.
  - Otherwise they're the same station when they share a normalized stream URL. A shared primary URL wins over a shared alternate. When that still leaves more than one candidate, the entry counts as a new station.
- A list that names the same station twice loads it once; the first entry wins.

Choices that go beyond the proposed defaults, for review:

- **The station ID must be a UUID.** An entry whose ID isn't a UUID is treated as having no ID.
  - Rejected alternative: readable slugs such as `radio-paradise-main`. They're friendlier to edit by hand, but unrelated lists can pick the same slug for different stations, and an ID match would then merge them.
- **A station without an ID takes the ID of the first entry that matches it.**
  - Example: the user added a station by its URL, and the catalog lists it later. The station takes the catalog's ID, so it still matches after the catalog changes the station's stream URL.
  - Rejected alternative: the app assigns its own IDs. An assigned ID can't match the same station in the catalog or on another device, so URL matching would still be needed, with one more ID to keep in sync.
- **When an entry matches several stations, it isn't merged with any of them.** A wrong merge would silently attach overrides and groups to the wrong station. A duplicate is visible, and the user can remove it.
- **Genres are a list.** Q10's default says "genre"; stations often have several, and search and filter (Feature 9) can use all of them.

Out of scope: writing lists (export, #5), loading and importing them (#5), and the catalog's repository and updates (#4).

## Capabilities

### New Capabilities
- `station-lists`: the station list format and station identity. That covers which entries a list yields, how stream URLs are normalized, and when two entries are the same station.

### Modified Capabilities

None.

## Impact

- New code in the core library from the project skeleton (#9): the station model, the list reader, URL normalization and station matching. The reader uses `System.Text.Json`, so there are no new dependencies.
- No UI. Later changes build on it: the catalog (#4), import and export (#5), and groups and overrides (#6).
- `doc/entrypoint/UserStory.md`: Q10 and Q11 are marked Resolved.
