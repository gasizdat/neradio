---
name: neradio-surgical-change
description: neradio's additions to surgical-change - spec scenarios pinned by tests, and changes verified on both Windows and Android. Use with surgical-change before writing any change to product source in this repo.
---

# Surgical changes in neradio

Follow `surgical-change` for the method. This adds what's specific to neradio.

## Verify like CI does

- Pin each OpenSpec `Scenario` the change implements with a test that mirrors its WHEN/THEN.
- A change is done only when it builds and passes on both targets, Windows and Android.
