NRobots.txt [![Build status](https://ci.appveyor.com/api/projects/status/ksog2wyr3y9v8m4i?svg=true)](https://ci.appveyor.com/project/AttilaKemny/nrobots-dotnet-core)
====

A robots.txt parser written in c#.

This is an unofficial fork of NRobots.txt on codeplex with extended functionality. For full documentation see https://nrobots.codeplex.com/.

## Project Description:
The Robots Exclusion Protocol or robots.txt protocol, is a convention to prevent cooperating web spiders and other web robots from accessing all or part of a website which is otherwise publicly viewable. 
This project provides an easy-to-use class, implemented in C#, to work with robots.txt files.

## Features:
* Loading Robots.txt files by providing Url or file content
* Async loading with cancellation, from a Url, Stream or TextReader
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

Each `LoadAsync` overload has a synchronous `Load` counterpart. Prefer `LoadAsync` for Urls: on .NET Framework and .NET Standard, `Load(Uri)` blocks a thread until the download completes.

## Thread safety:
A `Robots` instance can be queried from several threads at once, also while it is being reloaded. Each load replaces the previous rules in one step, so a query sees either the old rules or the new ones, never a mix. A load that fails or is cancelled keeps the previous rules.

Build with `RobotsBuilder` on one thread; share the result once it is built.

## What's new in 2.0.0:
* Targets `net10.0`, `netstandard2.0` and `net462`, replacing `net40`, `net451` and `netstandard1.1`.
* `LoadAsync` overloads for `Uri`, `Stream` and `TextReader`, each taking a `CancellationToken`.
* Downloads use `HttpClient` instead of the obsolete `WebRequest`, and a new `Robots(HttpClient)` constructor accepts your own client.

Breaking changes:
* `IRobots` has new `LoadAsync` members, so your own `IRobots` implementations must add them. Code that only calls `Robots` or `IRobots` needs no changes.
* A failed download throws `HttpRequestException` instead of `WebException`.
* Loading into an instance that already holds rules replaces them; 1.x added the new rules to the old ones.

## Building:
Requires the .NET 10 SDK (10.0.403 or later).

```
dotnet build Robots.slnx
dotnet test --solution Robots.slnx
```
