# Principle: what each test tier is for

neradio's tests split by what they need to be true to run, not by which project they cover. Put a
test in the wrong tier and it either can't prove what it claims, or it's slow and fragile for no
gain. Adapted from [GT4's principle](https://github.com/gasizdat/GT4/blob/master/doc/dev/principles/testing-strategy.md).

## The tiers

**Unit tests** (`Neradio.Core.Tests`): no MAUI, no device, no network. They run wherever
`dotnet test` runs, CI included, and they're the tier for logic: the player's rounds and waits,
title rules, playlist parsing, the sleep timer, ICY and HLS reading. They use:
- the fake engine behind the engine seam;
- `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`, so waits take no real time;
- a fake `HttpMessageHandler` that replays recorded stream data, so connections are tested without
  the network.

**Component tests** (`Neradio.UI.Tests`): the Razor components rendered with bUnit, against fake
services. No device and no WebView. The tier for what a screen shows and what a tap does.

**Hand checks** on a real phone and on Windows: what depends on the engine or the platform, such
as stream formats, background playback, the media session, calls and network switches. An emulator
doesn't reproduce power saving, calls or real network changes. Each implementation PR lists the
hand checks it ran, with their results.

## Rules

**Match the tier to what the test needs, not to where the code lives.** Logic that sits in the app
project but needs no device belongs in a unit test; move it down a layer if it can't be reached
from one.

**Pin each scenario and each prevented regression,** including assertions that something must not
happen, such as no retry after the user stops. Each OpenSpec scenario a change implements gets a
test that mirrors its WHEN/THEN, or a hand check where no test can reach it.

**Run the canonical entry point,** the one CI runs, not an ad-hoc subset: the affected test classes
first while working, then the whole suite before calling anything done.

## Non-goals

- Not a how-to-run reference: the commands live with the code and in CI.
- Not a test inventory: the solution is the live list of test projects.
