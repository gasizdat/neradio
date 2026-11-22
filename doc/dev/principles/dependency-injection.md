# Principle: dependency injection

neradio is built from small services, each with one job, behind an interface, and composed by
`Microsoft.Extensions.DependencyInjection`. This document gives the rules that make that worth more
than "we use DI". It's adapted from [GT4's principle](https://github.com/gasizdat/GT4/blob/master/doc/dev/principles/dependency-injection.md).

## One job per service, behind an interface

A service does one thing: one connection reads a stream, one parser reads ICY titles, one store
keeps the user's groups. When a service grows a second job, it becomes two services.

The interface lives in the lowest layer that needs it, usually `Neradio.Core`. The implementation
lives where its dependencies are allowed: the BASS engine in `Neradio.Engine.Bass`, the Android
media session in `Neradio.App`. So the core can depend on "something that decodes" or "something
that shows the media session" without knowing BASS or MAUI exist.

**Don't add an interface for its own sake.** An interface earns its place when there's a second
implementation (a fake in the tests counts) or when it hides a layer the caller mustn't know. A
helper with neither stays a plain class.

## Typed constructor parameters, never the service locator

A class declares what it needs as typed constructor parameters. It never takes `IServiceProvider`
and resolves things in its body: the constructor signature is the list of what the class depends
on, and the container absorbs the cost of building it.

Razor components can't take constructor parameters, so they use `[Inject]` properties
(`@inject` in markup). The same rule holds: inject the services the component uses, never
`IServiceProvider`.

## Keyed services replace switches

When a family of implementations is chosen by a value carried in data, the value becomes a DI key
(`AddKeyedSingleton<TInterface, TImpl>(key)`), not a `switch` the caller maintains. Adding a
case is then one registration where the others are.

- The caller gets a **resolver delegate**, for example `Func<StreamKind, IStreamReader>`, taken as
  a constructor parameter. It wraps `GetKeyedService` and never returns null: a missing key gives a
  registered fallback, such as "format not supported".
- If you find yourself writing `kind switch { … }` to pick an implementation, the keyed seam is
  being bypassed.

Likely first uses: the decoding path per content type on Windows (Windows' AAC decoder or BASS),
and the reader per stream kind (a plain stream or HLS).

## Values known only at runtime: a factory record

Some objects need a value only the caller has, such as the URL of a connection or the station a
dialog edits. The container can't supply that. For those, register a **factory record** that holds
everything the container can supply, with a `Create(...)` method that takes only what it can't:

```csharp
public sealed record Factory(HttpClient Http, TimeProvider Time, IIcyParser Icy)
{
    public Connection Create(Uri url) => new(Http, Time, Icy, url);
}
```

The factory is registered (`AddSingleton<Connection.Factory>()`), and callers take the factory as
a constructor parameter.

## neradio's shared services

- **`TimeProvider`**: registered once as `TimeProvider.System`. Everything that waits or measures
  time takes it, so the tests run on `FakeTimeProvider` and a 30-second wait takes no real time.
- **`HttpClient`**: one instance for the whole app, on a `SocketsHttpHandler` created explicitly.
  .NET's default handler on Android is the platform's HTTP stack, which Android's cleartext policy
  blocks for plain http (see the playback design). Never `new HttpClient()` elsewhere.
- **Platform services** (the media session, audio focus, network changes) are registered by
  `Neradio.App`, per platform, behind interfaces from `Neradio.Core`.

## Where registrations live

`Neradio.App` is the composition root: its startup registers every service. Each library offers
one extension method for its own services (for example `AddNeradioCore()`), so the app's startup
reads as a list of layers rather than a list of classes. That method is the live list of what a
layer registers; this document doesn't repeat it.

## Non-goals

- Not a registration reference: the `Add…()` methods are the live list.
- Not a lifetime guide for every service. The default is a singleton; a shorter lifetime needs a
  reason, such as state that belongs to one connection.
