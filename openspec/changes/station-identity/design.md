# Design

## Context

There's no code yet. The project skeleton (#9) creates the solution: a core library with no MAUI dependency, the app, and a test project. This change adds the first code to that core library. For the motivation, see proposal.md (Why); for the behavior, see `specs/station-lists/spec.md`.

Lists can be large. Radio Browser, a likely source for the catalog (#4), holds tens of thousands of stations.

## Goals / Non-Goals

**Goals:**
- One reader for every station list, whatever its source: the catalog, a loaded list, or an import.
- One component that decides "same station" and applies the ID rule. The list reader uses it to remove duplicates within a list, and later changes use it against all known stations.

**Non-Goals:**
- Writing lists (#5).
- Storing known stations between runs. Groups and overrides (#6) and import (#5) decide where stations are stored.
- Fetching lists over the network (#4, #5).

## Decisions

### Plain .NET code in the core library
Everything here is platform-neutral, so it goes in the core library. Tests run on Windows in CI, and the same assembly ships on Android.

Alternative: putting it in the MAUI app project. Rejected, because tests would then need the app's target frameworks.

### Walk a `JsonDocument` instead of deserializing into types
The spec skips one entry, or ignores one invalid value, without failing the rest. A typed `JsonSerializer` model stops at the first value of the wrong type. Getting the spec's behavior from it would take a custom converter for every field.

Walking a `JsonDocument` handles each field where it's read. It also ignores unknown fields without any extra code.

Alternative: source-generated `JsonSerializer` with lenient converters. Rejected: it means more code for the same behavior.

`System.Text.Json` is part of .NET, so the change adds no dependency.

### Normalize stream URLs through `System.Uri`
`Uri` already lowercases the host and drops a port that is the default for its scheme. The normalized form is:
- the host and any non-default port;
- the path with a trailing slash trimmed;
- the query.

The scheme is left out. Two normalized forms are compared ordinally.

An explicit `:443` on an http URL isn't that scheme's default port, so it stays, and the URL doesn't match `https://host/…`. That's what the spec asks for.

Alternative: hand-written string rules. Rejected: they'd have to reimplement host and port parsing.

### Parse IDs with `Guid.TryParseExact(value, "D")`
The "D" format is the 36-character hyphenated form the spec requires. A parsed `Guid` compares regardless of letter case, so no extra comparison code is needed.

Alternative: `Guid.TryParse`. Rejected: it also accepts the braced and the 32-digit forms, which the spec rules out.

### One "known stations" set owns matching and the ID rule
A set of stations, indexed by ID and by normalized stream URL, adds an entry in one step:
1. Look the entry up by ID.
2. If that finds nothing, collect the stations that share a stream URL with it, excluding those whose ID differs from the entry's.
3. Apply the tie-break: a shared primary stream wins; several candidates otherwise mean no match.
4. On a match, apply the ID rule: a station without an ID takes the entry's.

The step returns the matched station or the newly added one.

The list reader feeds each entry through a fresh set, which yields the spec's rule that the first entry of a list wins. Import (#5) and the catalog update (#4) will feed entries through the set of all known stations.

The indexes keep each step close to constant time. A linear scan over 50,000 stations for each of 50,000 entries would be too slow.

Alternative: a stateless `Matches(entry, station)` predicate that callers loop over. Rejected: every caller would repeat the tie-break and the ID rule, and the scan would be quadratic.

## Risks / Trade-offs

- [A network's shared fallback stream listed as an alternate by two different stations, both without IDs] → They merge into one station. The tie-break only helps when there are several candidates. That's accepted as rare, and the catalog's stations carry IDs.
- [The same station given as a `.pls`/`.m3u` URL in one list and as a direct stream URL in another] → They don't match, and the user sees a duplicate. Resolving playlist URLs while reading a list would need the network for every entry.
- [A list without IDs whose stream URLs carry rotating access tokens] → Each reload yields new stations. This follows from Q11: a different token makes a different station.
- [`Uri` canonicalizes the path, for example by resolving `.` and `..` segments] → A few URLs that differ only in such ways compare equal. That's harmless, since they reach the same resource.
