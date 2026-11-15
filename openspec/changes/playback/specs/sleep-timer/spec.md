# Spec Delta

## Purpose

Lets the user fall asleep to a station: playback stops on its own after a time the user chooses.

## ADDED Requirements

### Requirement: Setting the sleep timer
The user SHALL be able to set a sleep timer for a chosen number of minutes. While it runs, the app SHALL show the time left. When the time is up, playback SHALL stop, whether the station is playing or reconnecting.

#### Scenario: Timer runs out
- **WHEN** a station plays and the user sets the sleep timer to 30 minutes
- **THEN** playback stops 30 minutes later

#### Scenario: Time left is shown
- **WHEN** the user set the sleep timer to 30 minutes 10 minutes ago
- **THEN** the app shows 20 minutes left

#### Scenario: Timer runs out while reconnecting
- **WHEN** the time is up while the app is reconnecting
- **THEN** playback stops and the app makes no further connection attempts

#### Scenario: Screen off on Android
- **WHEN** the sleep timer runs out on an Android phone with the screen off
- **THEN** playback stops on time

### Requirement: Cancelling the sleep timer
The user SHALL be able to cancel the sleep timer. When the user stops playback, the timer SHALL be cancelled too. Playing another station, or a stop for a phone call, SHALL NOT cancel it.

#### Scenario: User cancels the timer
- **WHEN** the user cancels a running sleep timer
- **THEN** the station keeps playing past the time the timer would have run out

#### Scenario: User stops playback
- **WHEN** the user stops playback while the sleep timer runs, and plays a station again
- **THEN** no sleep timer runs

#### Scenario: Another station
- **WHEN** the user plays another station while the sleep timer runs
- **THEN** the timer keeps running, and the new station stops when it runs out

#### Scenario: Phone call
- **WHEN** a phone call stops playback on Android while the sleep timer runs
- **THEN** the timer keeps running, and the station plays again after the call only if the timer hasn't run out
