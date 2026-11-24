# station-lists Specification

## Purpose
Defines the station list format shared by the station catalog, the lists a user loads, and exports, and the rules that decide when two entries are the same station.

## Requirements

### Requirement: Station list document
A station list SHALL be a UTF-8 JSON object with an integer `version` and a `stations` array of station entries. A file that isn't such an object SHALL be rejected as a whole, and none of its stations loaded.

#### Scenario: Valid list loads its stations
- **WHEN** the app reads `{"version": 1, "stations": [...]}` holding three valid entries
- **THEN** it yields three stations, in list order

#### Scenario: File without a stations array is rejected
- **WHEN** the app reads a file that isn't JSON, or a JSON object without a `stations` array
- **THEN** the list is rejected and no stations are loaded

### Requirement: Format version
The app SHALL read lists of version 1. A list whose `version` is missing, isn't an integer, or is higher than the app supports SHALL be rejected as a whole. Adding a field SHALL NOT change the version.

#### Scenario: Newer version is rejected
- **WHEN** the app reads a list with `"version": 2`
- **THEN** the list is rejected and no stations are loaded

#### Scenario: Missing version is rejected
- **WHEN** the app reads a list without a `version` field
- **THEN** the list is rejected and no stations are loaded

### Requirement: Required station fields
Each station entry SHALL have a `name`: a string that isn't empty after trimming. It SHALL also have `streams`: an array of stream URLs. The first usable URL is the primary stream; the rest are alternates, kept in order.

#### Scenario: Primary and alternate streams
- **WHEN** an entry has `"streams": ["https://a.example/live", "https://b.example/live"]`
- **THEN** the station's primary stream is `https://a.example/live` and its only alternate is `https://b.example/live`

### Requirement: Optional station fields
A station entry can also have these fields, each optional:
- `id`: a station ID;
- `logo`: an absolute http or https URL;
- `genres`: an array of non-empty strings; any other element is dropped;
- `country`: an ISO 3166-1 alpha-2 code, two letters in any case, kept in upper case;
- `language`: a BCP 47 language tag, as a non-empty string;
- `bitrate`: a positive integer, in kbit/s.

A missing optional field SHALL leave the station without that value.

#### Scenario: Entry with only the required fields
- **WHEN** an entry has a `name` and `streams` and no other fields
- **THEN** the app yields a station with no ID, logo, genres, country, language or bitrate

#### Scenario: Entry with every field
- **WHEN** an entry has every optional field with a valid value
- **THEN** the station carries each of those values

#### Scenario: Country in lower case
- **WHEN** an entry has `"country": "us"`
- **THEN** the station's country is `US`

#### Scenario: Genres with unusable elements
- **WHEN** an entry has `"genres": ["jazz", "", 5]`
- **THEN** the station's only genre is `jazz`

### Requirement: Unknown fields are ignored
The app SHALL ignore fields it doesn't know, in the document and in station entries, and SHALL read the rest as if they weren't there.

#### Scenario: Entry with an unknown field
- **WHEN** an entry has a valid `name` and `streams`, plus a field `"homepage": "https://a.example"` the app doesn't know
- **THEN** the station loads as if the `homepage` field weren't there

### Requirement: Unusable entries are skipped
An entry without a usable name, or without a single usable stream URL, SHALL be skipped. The rest of the list SHALL still load. A usable stream URL is an absolute http or https URL. An unusable URL in an otherwise usable entry SHALL be dropped from its streams.

#### Scenario: Entry without a name
- **WHEN** a list holds three entries and the second has no `name`
- **THEN** the app yields the first and third stations

#### Scenario: Entry without a usable stream
- **WHEN** an entry's only stream is `ftp://a.example/live`
- **THEN** the entry is skipped

#### Scenario: Unusable stream among usable ones
- **WHEN** an entry has `"streams": ["not a url", "https://a.example/live"]`
- **THEN** the station's primary stream is `https://a.example/live` and it has no alternates

### Requirement: Invalid optional values are ignored
An optional field whose value isn't valid SHALL be ignored, as if it were missing. The entry itself SHALL still load.

#### Scenario: Invalid logo
- **WHEN** an entry has `"logo": "ftp://a.example/logo.png"`
- **THEN** the station loads without a logo

#### Scenario: Invalid bitrate
- **WHEN** an entry has `"bitrate": -5`
- **THEN** the station loads without a bitrate

### Requirement: Station IDs are UUIDs
A station ID SHALL be a UUID in its 36-character hyphenated form, such as `5f0c2b7e-3d4a-4c1b-9e8f-0a1b2c3d4e5f`. Two IDs SHALL be compared without regard to letter case. An `id` in any other form SHALL be ignored, and the entry treated as having no ID.

#### Scenario: IDs differing only in letter case
- **WHEN** one entry's ID is `5F0C2B7E-3D4A-4C1B-9E8F-0A1B2C3D4E5F` and another's is the same in lower case
- **THEN** the two IDs are equal

#### Scenario: ID that isn't a UUID
- **WHEN** an entry has `"id": "42"`
- **THEN** the station loads as having no ID

#### Scenario: UUID in another form
- **WHEN** an entry has `"id": "{5f0c2b7e-3d4a-4c1b-9e8f-0a1b2c3d4e5f}"`
- **THEN** the station loads as having no ID

### Requirement: Normalized stream URL
Comparing two stream URLs SHALL ignore:
- the scheme (http or https);
- the letter case of the host;
- an explicit port that is the default for the URL's scheme;
- a trailing slash on the path.

Every other part SHALL match exactly, including the path's letter case, a non-default port and the query.

#### Scenario: http and https
- **WHEN** one stream URL is `http://a.example/live` and another is `https://a.example/live`
- **THEN** they are the same stream URL

#### Scenario: Host case, default port and trailing slash
- **WHEN** one stream URL is `https://A.Example:443/live/` and another is `https://a.example/live`
- **THEN** they are the same stream URL

#### Scenario: Different host
- **WHEN** one stream URL is `https://a.example/live` and another is `https://mirror.a.example/live`
- **THEN** they are different stream URLs

#### Scenario: Different query
- **WHEN** one stream URL is `https://a.example/live?token=1` and another is `https://a.example/live?token=2`
- **THEN** they are different stream URLs

#### Scenario: Different path case
- **WHEN** one stream URL is `https://a.example/Live` and another is `https://a.example/live`
- **THEN** they are different stream URLs

### Requirement: Matching by ID
When an entry and a known station both have IDs, they SHALL be the same station exactly when their IDs are equal, whatever their stream URLs. An entry that matches a known station by ID SHALL NOT match any other station by stream URL. A known station is one the app already has, from the catalog, another list or the user.

#### Scenario: ID match wins over a stream match
- **WHEN** an entry has the same ID as one known station and shares a stream URL with another known station that has no ID
- **THEN** the entry is the station with its ID, and not the other one

#### Scenario: Same ID, different streams
- **WHEN** an entry has the same ID as a known station but a different stream URL
- **THEN** the entry is that station

#### Scenario: Different IDs, same stream
- **WHEN** an entry has a different ID from a known station but the same stream URL
- **THEN** the entry isn't that station

### Requirement: Matching by stream URL
When an entry or a known station has no ID, they SHALL be the same station when they share a normalized stream URL, primary or alternate. When the entry shares stream URLs with several known stations, "Ambiguous matches make a new station" decides instead.

#### Scenario: Entry's alternate is the station's primary
- **WHEN** an entry without an ID has an alternate stream that is a known station's primary stream
- **THEN** the entry is that station

#### Scenario: Entry with an ID, station without one
- **WHEN** an entry with an ID shares a stream URL with a known station that has no ID
- **THEN** the entry is that station

### Requirement: Ambiguous matches make a new station
When an entry matches several known stations by stream URL, the one whose primary stream equals the entry's primary stream SHALL be the match, if there's exactly one. Otherwise the entry SHALL match none of them.

#### Scenario: One candidate shares the primary stream
- **WHEN** an entry matches two known stations by stream URL, and only one of them has the entry's primary stream as its primary
- **THEN** the entry is that one station

#### Scenario: No single candidate shares the primary stream
- **WHEN** an entry matches two known stations, each by an alternate stream only
- **THEN** the entry matches neither and counts as a new station

### Requirement: A station without an ID takes the matching entry's ID
When an entry with an ID matches a known station without one, the station SHALL take the entry's ID, and keep it from then on.

#### Scenario: Station added by URL appears in the catalog
- **WHEN** a known station without an ID matches a catalog entry with an ID by stream URL
- **THEN** the station takes the catalog entry's ID
- **AND** a later catalog entry with that ID but a different stream URL still matches it

#### Scenario: Second entry with a different ID
- **WHEN** a station has taken one entry's ID, and a second entry with a different ID shares its stream URL
- **THEN** the second entry isn't that station

### Requirement: Each station appears once per list
A list SHALL yield each station once. An entry that matches an earlier entry of the same list SHALL be skipped, and the earlier entry's values kept. The only exception is the ID rule: an earlier entry without an ID takes the later entry's ID.

#### Scenario: Same ID twice
- **WHEN** a list has two entries with the same ID and different names
- **THEN** it yields one station, with the first entry's name

#### Scenario: Two entries without IDs sharing a stream
- **WHEN** a list has two entries without IDs that share a stream URL
- **THEN** it yields one station, from the first entry

#### Scenario: Earlier entry without an ID
- **WHEN** a list has an entry without an ID, followed by an entry with an ID and a different name that shares its stream URL
- **THEN** it yields one station, with the first entry's name and the second entry's ID
