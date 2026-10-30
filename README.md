NRobots.txt [![Build status](https://ci.appveyor.com/api/projects/status/ksog2wyr3y9v8m4i?svg=true)](https://ci.appveyor.com/project/AttilaKemny/nrobots-dotnet-core)
====

A robots.txt parser written in c#.

This is an unofficial fork of NRobots.txt on codeplex with extended functionality. For full documentation see https://nrobots.codeplex.com/.

Upgrading from 1.x? See the [migration guide](MIGRATION.md).

## Project Description:
The Robots Exclusion Protocol or robots.txt protocol, is a convention to prevent cooperating web spiders and other web robots from accessing all or part of a website which is otherwise publicly viewable. 
This project provides an easy-to-use class, implemented in C#, to work with robots.txt files.

## Features:
* Loading Robots.txt files by providing Url or file content
* Async loading with cancellation, from a Url, Stream or TextReader
* Follows RFC 9309 when downloading: status codes, size limit
* Easy-to-use and simple usage
* Fluent interface
* Supports multiple User-Agents
* Supports different types of entries:
* Disallow entries
* Allow entries
* Sitemap entries
* Crawl-delay entries
* Supports comments
* Supports wild cards (both * and $)

## Supported frameworks:
* .NET 10 (`net10.0`)
* .NET Standard 2.0 (`netstandard2.0`), for .NET Core, Mono, Unity and others
* .NET Framework 4.6.2 and later (`net462`)

## Usage:
```csharp
// Downloads https://www.example.com/robots.txt
var robots = new Robots.Robots();
await robots.LoadAsync(new Uri("https://www.example.com"), cancellationToken);

bool allowed = robots.Allowed("/private/page.html", "mybot");
int crawlDelay = robots.GetCrawlDelay("mybot");
IList<string> sitemaps = robots.GetSitemapUrls();
```

`new Robots.Robots()` shares one `HttpClient` between instances. To use your own, for example one from `IHttpClientFactory`, pass it to the constructor:

```csharp
var robots = new Robots.Robots(httpClientFactory.CreateClient());
```

robots.txt content you already have can be loaded from a string, `Stream` or `TextReader`:

```csharp
robots.LoadContent("User-agent: *\nDisallow: /private", "https://www.example.com");
await robots.LoadAsync(stream, "https://www.example.com", cancellationToken);
```

Each load replaces the rules loaded before. To add rules to them instead, for example your own rules on top of a site's robots.txt, use `AddContent`, `Add` or `AddAsync`:

```csharp
await robots.LoadAsync(new Uri("https://www.example.com"), cancellationToken);
robots.AddContent("User-agent: *\nDisallow: /search");
```

Each `LoadAsync` overload has a synchronous `Load` counterpart. Prefer `LoadAsync` for Urls: on .NET Framework and .NET Standard, `Load(Uri)` blocks a thread until the download completes.

### User-Agent:
Downloads send the `User-Agent` header `NRobotsCore/{version}`. Identify your crawler instead by setting `UserAgent`:

```csharp
var robots = new Robots.Robots { UserAgent = "MyCrawler/1.0 (+https://example.com/crawler)" };
```

With your own `HttpClient`, `UserAgent` starts as `null` and the client's `DefaultRequestHeaders` apply; setting `UserAgent` overrides them for robots.txt requests. `UserAgent` is only the HTTP header: to check the rules for your crawler, pass its name to `Allowed`.

### Downloading (RFC 9309):
* **2xx**: the rules are parsed.
* **4xx**, such as 404: there is no robots.txt, so everything is allowed.
* **5xx**: the rules are unknown, so everything is disallowed. Try again later.
* A redirect the `HttpClient` doesn't follow, or a network error, throws `HttpRequestException`.
* Only the first 500 KiB is parsed; a line cut off by the limit is ignored. This doesn't apply to `Stream`, `TextReader` or string content you load yourself.

## Thread safety:
A `Robots` instance can be queried from several threads at once, also while rules are being loaded or added. A load replaces the rules and an add extends them, each in one step once the whole file has been read, so a query sees either the rules from before or after, never a mix. A load or add that fails or is cancelled keeps the rules from before.

Build with `RobotsBuilder` on one thread; share the result once it is built.

## What's new in 2.0.0:
* Targets `net10.0`, `netstandard2.0` and `net462`, replacing `net40`, `net451` and `netstandard1.1`.
* `LoadAsync` overloads for `Uri`, `Stream` and `TextReader`, each taking a `CancellationToken`.
* Downloads use `HttpClient` instead of the obsolete `WebRequest`, and a new `Robots(HttpClient)` constructor accepts your own client.
* `AddContent`, `Add` and `AddAsync` add rules to the loaded ones.
* Downloads follow RFC 9309 for status codes and size, and send a configurable `User-Agent`.
* Safe to query from several threads, also while loading or adding rules.
* Annotated for nullable reference types, and marked trimmable and AOT-compatible on .NET 10.

Breaking changes:
* `IRobots` has new `LoadAsync` and `Add` members, so your own `IRobots` implementations must add them. Code that only calls `Robots` or `IRobots` needs no changes.
* A failed download throws `HttpRequestException` instead of `WebException`.
* A 4xx response allows everything and a 5xx response disallows everything, where 1.x threw `WebException`.
* Loading into an instance that already holds rules replaces them; 1.x added the new rules to the old ones. Use `AddContent`, `Add` or `AddAsync` for the 1.x behaviour.
* Nullable annotations: `BaseUri`, `UserAgent` and some model properties are nullable, and a few misuses throw `InvalidOperationException` instead of `NullReferenceException` or `ArgumentNullException`.

The [migration guide](MIGRATION.md) explains each change with examples.

## Building:
Requires the .NET 10 SDK (10.0.403 or later). The build treats warnings as errors, with nullable reference types and the recommended .NET analyzers enabled.

```
dotnet build Robots.slnx
dotnet test --solution Robots.slnx
```
