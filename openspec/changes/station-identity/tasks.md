# Tasks

Implementation starts once the project skeleton (#9) is on `main`. Everything goes in its core library and test project. Each scenario in `specs/station-lists/spec.md` gets a test that mirrors its WHEN/THEN.

## 1. Station model, IDs and stream URLs

- [ ] 1.1 Add the station model: ID, name, logo, primary and alternate streams, genres, country, language and bitrate. Verify the solution builds for every target.
- [ ] 1.2 Add ID parsing in the 36-character hyphenated form. Verify that tests pin every scenario of "Station IDs are UUIDs".
- [ ] 1.3 Add stream URL normalization through `System.Uri`, as in design.md. Verify that tests pin every scenario of "Normalized stream URL", including the ones where two URLs must not compare equal.

## 2. Known stations set

- [ ] 2.1 Add the known stations set, indexed by ID and by normalized stream URL. Adding an entry does three things: it matches by ID first, then by stream URL; it applies the primary-stream tie-break; and it applies the ID rule. Verify that tests pin every scenario of these requirements:
  - "Matching by ID"
  - "Matching by stream URL"
  - "Ambiguous matches make a new station"
  - "A station without an ID takes the matching entry's ID"

  The tests include the cases where an entry must not match.

## 3. List reader

- [ ] 3.1 Read the document by walking a `JsonDocument`, as in design.md: the `version` check and the `stations` array. Verify that tests pin every scenario of "Station list document" and "Format version".
- [ ] 3.2 Read station entries. Verify that tests pin every scenario of these requirements:
  - "Required station fields"
  - "Optional station fields"
  - "Unknown fields are ignored"
  - "Unusable entries are skipped"
  - "Invalid optional values are ignored"
- [ ] 3.3 Feed each entry through a fresh known stations set, so the first entry of a list wins. Verify that tests pin every scenario of "Each station appears once per list".

## 4. Verification

- [ ] 4.1 Run the CI entry point from #9: first the tests added here, then the full suite. Verify that the build and the tests pass on both Windows and Android.

## Workflow follow-up

- The implementation PR says `Fixes #3` and archives this change, which creates `openspec/specs/station-lists/spec.md`.
- After archiving, `openspec validate --all` passes.
