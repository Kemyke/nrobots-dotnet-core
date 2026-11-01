# Migrating from 1.x to 2.x

NRobotsCore 2.0 keeps every 1.x method signature, so most code keeps compiling unchanged. The changes are in which frameworks are supported, how rules are matched (now RFC 9309), how downloads behave, how repeated loads combine, the `IRobots` interface, and nullable annotations. This guide lists what to check, with examples.

## Checklist

| If your code... | Then |
|---|---|
| Targets .NET Framework 4.0–4.6.1 or .NET Standard 1.x | [Move to a supported framework](#supported-frameworks) |
| Depends on which Urls `Allowed` allows | [Check the RFC 9309 matching changes](#matching-follows-rfc-9309) |
| Implements `IRobots` itself (including test fakes) | [Add the new members](#irobots-has-new-members) |
| Reads `GetCrawlDelay` or `CrawlDelayEntry` | [Check the crawl-delay changes](#crawl-delay) |
| Catches `WebException` around `Load(Uri)` | [Catch `HttpRequestException` instead](#exceptions) |
| Treats a 404 or 5xx error from `Load(Uri)` as "no rules" or retries on it | [Check the new status handling](#http-status-codes-rfc-9309) |
| Calls `Load` more than once on the same instance to combine files | [Use `AddContent` for the extra files](#adding-rules-to-the-loaded-ones) |
| Loads several sites into one instance | [Use one instance per site](#one-instance-per-site) |
| Loads from a Url in a server or UI app | [Use `LoadAsync`](#prefer-loadasync) |
| Passes its own `HttpClient` with a custom handler and calls `Load(Uri)` | [Check the handler supports `Send`](#your-own-httpclient) |
| Filters or logs requests by User-Agent | [A User-Agent is now sent](#user-agent) |
| Has nullable reference types enabled | [Check the new annotations](#nullable-annotations) |
| Catches `NullReferenceException` or `ArgumentNullException` from `Allowed` or `RobotsBuilder` | [Check the smaller behaviour changes](#smaller-behaviour-changes) |
| Creates or derives from `UserAgents` or `BrowserUserAgents` | [Use their constants directly](#smaller-behaviour-changes) |

## Supported frameworks

| 1.x | 2.x |
|---|---|
| `net40`, `net451` | `net462` (.NET Framework 4.6.2 and later) |
| `netstandard1.1` | `netstandard2.0` |
| | `net10.0` |

Projects on .NET Framework 4.0–4.6.1 or on platforms that only support .NET Standard 1.x must stay on 1.x or move to a supported framework.

The assembly version is now 2.0.0.0, and the assembly carries the package's authors and copyright instead of the old template values.

On .NET 10 the library is marked trimmable and AOT-compatible, so it can be used in trimmed and Native AOT apps without warnings.

## IRobots has new members

`IRobots` gained `LoadAsync` methods, methods that [add rules to the loaded ones](#adding-rules-to-the-loaded-ones), `Encoding` overloads for streams, and read-only access to what was parsed:

```csharp
Task LoadAsync(Uri robotsUri, CancellationToken cancellationToken = default(CancellationToken));
Task LoadAsync(Stream stream, string baseUrl, CancellationToken cancellationToken = default(CancellationToken));
Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken));
Task LoadAsync(TextReader reader, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken));

void AddContent(string fileContent);
void Add(Stream stream);
Task AddAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken));
void Add(TextReader reader);
Task AddAsync(TextReader reader, CancellationToken cancellationToken = default(CancellationToken));

void Load(Stream stream, string baseUrl, Encoding? encoding);
void Load(Stream stream, Uri baseUri, Encoding? encoding);
Task LoadAsync(Stream stream, string baseUrl, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken));
Task LoadAsync(Stream stream, Uri baseUri, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken));
void Add(Stream stream, Encoding? encoding);
Task AddAsync(Stream stream, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken));

RobotsSnapshot Snapshot { get; }
IReadOnlyList<RobotsGroup> Groups { get; }
IReadOnlyList<string> Sitemaps { get; }
IReadOnlyList<RobotsParseError> ParseErrors { get; }
RobotsGroup? GetGroup(string userAgent);
bool TryGetCrawlDelay(out TimeSpan crawlDelay);
bool TryGetCrawlDelay(string userAgent, out TimeSpan crawlDelay);
```

Code that only *calls* `IRobots` is unaffected. A class that *implements* it, such as a test fake, must add them. `RobotsSnapshot`, `RobotsGroup` and `RobotsParseError` lists come from a real `Robots`, so a fake can keep one for the members it doesn't fake:

```csharp
public class FakeRobots : IRobots
{
    private readonly Robots.Robots _real = new Robots.Robots();

    // ... the 1.x members as before; with nullable enabled, declare BaseUri as Uri? ...

    public RobotsSnapshot Snapshot => _real.Snapshot;
    public IReadOnlyList<RobotsGroup> Groups => _real.Groups;
    public IReadOnlyList<string> Sitemaps => _real.Sitemaps;
    public IReadOnlyList<RobotsParseError> ParseErrors => _real.ParseErrors;
    public RobotsGroup? GetGroup(string userAgent) => _real.GetGroup(userAgent);
    public bool TryGetCrawlDelay(out TimeSpan crawlDelay) => _real.TryGetCrawlDelay(out crawlDelay);
    public bool TryGetCrawlDelay(string userAgent, out TimeSpan crawlDelay) => _real.TryGetCrawlDelay(userAgent, out crawlDelay);

    public void Load(Stream stream, string baseUrl, Encoding? encoding) => Load(stream, baseUrl);
    public void Load(Stream stream, Uri baseUri, Encoding? encoding) => Load(stream, baseUri);
    public Task LoadAsync(Stream stream, string baseUrl, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken)) => LoadAsync(stream, baseUrl, cancellationToken);
    public Task LoadAsync(Stream stream, Uri baseUri, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken)) => LoadAsync(stream, baseUri, cancellationToken);
    public void Add(Stream stream, Encoding? encoding) => Add(stream);
    public Task AddAsync(Stream stream, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken)) => AddAsync(stream, cancellationToken);

    public Task LoadAsync(Uri robotsUri, CancellationToken cancellationToken = default(CancellationToken))
    {
        Load(robotsUri);
        return Task.CompletedTask;
    }

    public Task LoadAsync(Stream stream, string baseUrl, CancellationToken cancellationToken = default(CancellationToken))
    {
        Load(stream, baseUrl);
        return Task.CompletedTask;
    }

    public Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken))
    {
        Load(stream, baseUri);
        return Task.CompletedTask;
    }

    public Task LoadAsync(TextReader reader, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken))
    {
        Load(reader, baseUri);
        return Task.CompletedTask;
    }

    public void AddContent(string fileContent) { /* add the rules to your fake's state */ }

    public void Add(Stream stream) { /* ... */ }

    public Task AddAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
    {
        Add(stream);
        return Task.CompletedTask;
    }

    public void Add(TextReader reader) { /* ... */ }

    public Task AddAsync(TextReader reader, CancellationToken cancellationToken = default(CancellationToken))
    {
        Add(reader);
        return Task.CompletedTask;
    }
}
```

## Exceptions

1.x downloaded with `WebRequest`; 2.x uses `HttpClient`, so different exceptions are thrown:

| Failure | 1.x | 2.x |
|---|---|---|
| Network error (DNS, connection refused) | `WebException` | `HttpRequestException` |
| Redirect the client doesn't follow | `WebException` | `HttpRequestException` |
| 4xx or 5xx status | `WebException` | No exception, see [status codes](#http-status-codes-rfc-9309) |
| Timeout (`HttpClient.Timeout`, 100 seconds by default) | `WebException` | `TaskCanceledException` |
| Your `CancellationToken` cancelled | n/a | `OperationCanceledException` |

Before:

```csharp
try
{
    robots.Load(new Uri("https://www.example.com"));
}
catch (WebException e)
{
    logger.LogWarning(e, "Could not load robots.txt");
}
```

After:

```csharp
try
{
    await robots.LoadAsync(new Uri("https://www.example.com"), cancellationToken);
}
catch (HttpRequestException e)
{
    logger.LogWarning(e, "Could not load robots.txt");
}
catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
{
    logger.LogWarning(e, "Timed out loading robots.txt");
}
```

## HTTP status codes (RFC 9309)

1.x threw `WebException` for every error status. 2.x follows [RFC 9309](https://www.rfc-editor.org/rfc/rfc9309), the robots.txt standard:

| Response | 2.x behaviour |
|---|---|
| 2xx | The rules are parsed. |
| 4xx, such as 404 Not Found or 403 Forbidden | No robots.txt: everything is allowed. |
| 5xx | Rules unknown: everything is disallowed. |

Code that caught the exception for a missing robots.txt can drop that handling:

```csharp
// 1.x
bool hasRules;
try
{
    robots.Load(siteUri);
    hasRules = true;
}
catch (WebException e) when ((e.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
{
    hasRules = false; // no robots.txt: crawl everything
}

// 2.x: a 404 loads "allow everything", so no special case is needed
await robots.LoadAsync(siteUri, cancellationToken);
```

A 5xx no longer throws either, so code that retried on the exception needs another signal. While the server fails, every path is disallowed; load again later:

```csharp
await robots.LoadAsync(siteUri, cancellationToken);
if (!robots.Allowed("/"))
{
    // Either the site disallows everything, or its server failed (5xx). Try again later.
}
```

Only the first 500 KiB of a downloaded robots.txt is parsed, as RFC 9309 allows. A line cut off by the limit is ignored. Content you load yourself from a `Stream`, `TextReader` or string isn't limited.

## Load replaces the rules, Add adds to them

In 1.x, loading into an instance that already held rules added the new rules to the old ones. In 2.x, `Load`, `LoadAsync` and `LoadContent` replace them, and the new `AddContent`, `Add` and `AddAsync` [add to them](#adding-rules-to-the-loaded-ones) the way 1.x did:

```csharp
var robots = new Robots.Robots();
robots.LoadContent("User-agent: *\nDisallow: /a", "https://www.example.com");
robots.LoadContent("User-agent: *\nDisallow: /b", "https://www.example.com");

robots.Allowed("/a"); // 1.x: false (rules from both files). 2.x: true (only the second file)
robots.Allowed("/b"); // false in both
```

A load or add that fails or is cancelled now keeps the rules from before, where 1.x could leave them half-loaded.

### Adding rules to the loaded ones

To keep the 1.x behaviour, load the first file and add the others with `AddContent`, `Add` or `AddAsync`. The rules combine the same way as loading them one after another did in 1.x; they are then matched by the [RFC 9309 rules](#matching-follows-rfc-9309):

```csharp
// 1.x
robots.LoadContent(siteRules, "https://www.example.com");
robots.LoadContent(extraRules, "https://www.example.com");

// 2.x
robots.LoadContent(siteRules, "https://www.example.com");
robots.AddContent(extraRules);
```

The same works after a download, for example to add your own rules to a site's robots.txt:

```csharp
var robots = new Robots.Robots();
await robots.LoadAsync(new Uri("https://www.example.com"), cancellationToken);

robots.AddContent("User-agent: *\nDisallow: /search");   // never crawl the site's search pages

using (var stream = File.OpenRead("extra-rules.txt"))
{
    await robots.AddAsync(stream, cancellationToken);
}
```

How added rules combine with the loaded ones:

* A group for an agent that already has one gets the new rules appended, so both files' rules apply. For a `Crawl-delay` in both, the first one wins.
* Groups for new agents, comments and sitemaps are added after the existing ones.
* Paths resolve against the current `BaseUri`, which doesn't change. In 1.x, `BaseUri` became the second load's base URL; that only matters if your two loads used different base URLs.
* Adding needs a `BaseUri`: load first or set `BaseUri`, otherwise `InvalidOperationException` is thrown.

Use `AddContent` rather than joining the texts yourself: joined text isn't always the same. A rule before the first `User-agent` line of the second file is ignored by 1.x and by `AddContent`, but in joined text it belongs to the first file's last group:

```csharp
robots.LoadContent("User-agent: *\nDisallow: /a", "https://www.example.com");
robots.AddContent("Disallow: /b");
robots.Allowed("/b"); // true, as in 1.x

robots.LoadContent("User-agent: *\nDisallow: /a\nDisallow: /b", "https://www.example.com"); // joined text
robots.Allowed("/b"); // false
```

### One instance per site

A robots.txt only applies to its own site. 1.x let you load two sites into one instance, but then the second site's base URL replaced the first's and each site's rules applied to the other's paths. Keep one instance per site instead:

```csharp
var robotsBySite = new Dictionary<string, Robots.Robots>(StringComparer.OrdinalIgnoreCase);

foreach (var url in urlsToCrawl)
{
    // "https://www.example.com" for "https://www.example.com/products/1"
    string site = url.GetLeftPart(UriPartial.Authority);

    if (!robotsBySite.TryGetValue(site, out var robots))
    {
        robots = new Robots.Robots { UserAgent = "MyCrawler/1.0 (+https://example.com/crawler)" };
        await robots.LoadAsync(url, cancellationToken); // downloads {site}/robots.txt
        robotsBySite.Add(site, robots);
    }

    if (robots.Allowed(url, "MyCrawler"))
    {
        // crawl url
    }
}
```

The dictionary isn't thread-safe; a crawler that loads from several threads needs a `ConcurrentDictionary` or a lock around it. A `Robots` instance itself is safe to query from any thread.

## Matching follows RFC 9309

1.x matched rules with its own segment-based logic. 2.x follows [RFC 9309](https://www.rfc-editor.org/rfc/rfc9309.html), section 2.2, the same rules Google's crawler uses, so some Urls get a different answer:

| robots.txt rules | Url | 1.x | 2.x |
|---|---|---|---|
| `Disallow: /Private` | `/private` | disallowed | **allowed**: paths are case-sensitive |
| `Allow: /p` and `Disallow: /p/q` | `/p/q` | allowed | **disallowed**: the longest rule wins, whatever the order |
| `Disallow: /folder/` | `/folder` | disallowed | **allowed**: the rule needs the trailing `/` |
| `Disallow: /*.pdf$` | `/files/a.pdf` | allowed | **disallowed**: `*` matches any characters, `$` anchors the end |
| `Disallow: /fish*` | `/fishheads` | allowed | **disallowed** |
| `Disallow: /a/*$` | `/a/b/c/` | allowed | **disallowed**: `/a/` followed by anything |
| `Disallow: /` | `/robots.txt` | disallowed | **allowed**: robots.txt itself is always allowed |
| `User-agent : FooBot` (space before `:`) | any | rule ignored | **applies** |

The agent you pass to `Allowed` also matches differently. Only its product token counts: the letters, `_` and `-` it starts with. So `"ExampleBot/2.1 (+https://example.com/bot)"` finds the `ExampleBot` group, where 1.x needed exactly `"ExampleBot"`. A browser-style string such as `"Mozilla/5.0 (compatible; ExampleBot/2.1)"` starts with `Mozilla`, so pass the token itself:

```csharp
robots.LoadContent("User-agent: ExampleBot\nDisallow: /private", "https://www.example.com");

robots.Allowed("/private/a", "ExampleBot");                    // false
robots.Allowed("/private/a", "ExampleBot/2.1 (+https://ex.com)"); // 1.x: true (no matching group). 2.x: false
```

Groups for the same product token are combined, so `User-agent: ExampleBot` and `User-agent: examplebot/1.0` both apply to `"ExampleBot"`.

If your crawler depended on a 1.x answer, check the table above: under RFC 9309 a site that wants `/folder` blocked writes `Disallow: /folder`, which covers `/folder` and everything below it.

## Prefer LoadAsync

Every `Load` method has a `LoadAsync` counterpart that takes a `CancellationToken`:

```csharp
// 1.x
robots.Load(new Uri("https://www.example.com"));

// 2.x
await robots.LoadAsync(new Uri("https://www.example.com"), cancellationToken);
```

`Load(Uri)` still works. On .NET 10 it uses `HttpClient`'s synchronous API, but on .NET Framework and .NET Standard it blocks a thread until the download completes. In ASP.NET and UI apps, use `LoadAsync`.

## Your own HttpClient

`new Robots()` uses one shared `HttpClient` with the default timeout of 100 seconds. To use your own, for example from `IHttpClientFactory`, or to set a proxy or a shorter timeout, pass it in. `Robots` never disposes a client you pass:

```csharp
services.AddHttpClient("robots", client => client.Timeout = TimeSpan.FromSeconds(10));

var robots = new Robots.Robots(httpClientFactory.CreateClient("robots"));
```

On .NET 10, the synchronous `Load(Uri)` calls `HttpClient.Send`. If your client uses a custom `HttpMessageHandler` that derives from `HttpMessageHandler` directly, it must override `Send`, or `Load(Uri)` throws `NotSupportedException`; `LoadAsync` only needs `SendAsync`. Handlers based on `DelegatingHandler` forward `Send` already.

```csharp
public class LoggingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // ...
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Needed for the synchronous Load(Uri) on .NET 5 and later
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
```

## User-Agent

1.x sent no `User-Agent` header. 2.x sends `NRobotsCore/{version}` with the built-in client. Identify your crawler instead:

```csharp
var robots = new Robots.Robots { UserAgent = "MyCrawler/1.0 (+https://example.com/crawler)" };
```

With your own `HttpClient`, `UserAgent` starts as `null` and the client's `DefaultRequestHeaders.UserAgent` is sent, as in 1.x; setting `UserAgent` overrides it for robots.txt requests. An invalid value throws `ArgumentException` when set.

`UserAgent` is only the HTTP header. To check rules for your crawler, still pass its name to `Allowed`:

```csharp
bool allowed = robots.Allowed("/page.html", "MyCrawler");
```

## Crawl-delay

1.x read `Crawl-delay` as a whole number, so `Crawl-delay: 1.5` became 0 and the site's delay was lost. 2.x reads seconds with fractions, the same way on every machine:

| `Crawl-delay:` | 1.x `GetCrawlDelay` | 2.x `GetCrawlDelay` | 2.x `TryGetCrawlDelay` |
|---|---|---|---|
| `5` | 5 | 5 | 5 s |
| `1.5` | 0 | **2** (rounded up) | 1.5 s |
| `0.1` | 0 | **1** (rounded up) | 0.1 s |
| `1e3` | 0 | **1000** | 1000 s |
| `-1` | -1 | 0, and reported in `ParseErrors` | `false` |
| `1,5`, `NaN`, empty | 0 | 0, and reported in `ParseErrors` | `false` |

`GetCrawlDelay` rounds up so a crawler never waits less than the site asked. Use `TryGetCrawlDelay` for the exact value:

```csharp
// 1.x
int delayInSeconds = robots.GetCrawlDelay("MyCrawler");

// 2.x, exact
TimeSpan delay = robots.TryGetCrawlDelay("MyCrawler", out var crawlDelay) ? crawlDelay : TimeSpan.Zero;
```

An invalid `Crawl-delay` no longer counts as 0. In 1.x, `Crawl-delay: soon` followed by `Crawl-delay: 3` gave 0, because the group's first `Crawl-delay` won; in 2.x the invalid line is skipped and the result is 3.

`CrawlDelayEntry` keeps `int CrawlDelay`, now rounded up, and gains `TimeSpan Delay` with the exact value; setting either one sets the other. `Entry.TryParse` returns `false` for an invalid `Crawl-delay`, where 1.x returned an entry with 0.

## Nullable annotations

2.x is annotated for nullable reference types. If your project has `<Nullable>enable</Nullable>`, the compiler now tells you where a value can be null:

| Member | Type | Null when |
|---|---|---|
| `Robots.BaseUri`, `IRobots.BaseUri` | `Uri?` | Nothing has been loaded and it hasn't been set |
| `Robots.UserAgent` | `string?` | You passed your own `HttpClient`, or set it to null |
| `Entry.Comment` | `string?` | The line had no comment |
| `UrlEntry.Url` | `Uri?` | The rule's path isn't a valid relative URL, or you create an entry yourself without one |
| `UserAgentEntry.CrawlDelayEntry`, `UserAgentEntry.SitemapEntry` | nullable | The group has no such line |
| `Entry.TryParse(..., out Entry? entry)` | `[NotNullWhen(true)]` | `entry` is not null when it returns true |

```csharp
// BaseUri is null until something is loaded or it is set
Uri? baseUri = robots.BaseUri;

if (baseUri != null && Entry.TryParse(baseUri, line, out var entry))
{
    Console.WriteLine(entry.Type); // no warning: entry is not null here
}
```

`UserAgentEntry.UserAgent` and `SitemapEntry.SitemapUrl` now start as an empty string instead of null.

## Smaller behaviour changes

* `Crawl-delay` values are parsed the same way on every machine. 1.x used the current culture.
* Checking a relative Url with `Allowed` on an instance that has rules but no `BaseUri`, for example after setting `BaseUri` to null, throws `InvalidOperationException` with a clear message, instead of `ArgumentNullException`. An instance without rules still allows everything.
* `RobotsBuilder.Allow` or `Disallow` before `ForUserAgent` throws `InvalidOperationException`, instead of `NullReferenceException`.
* An empty `Disallow:` (or `Allow:`) ends a group's `User-agent` lines, as RFC 9309 and Google's parser do. In `User-agent: a`, `Disallow:`, `User-agent: b`, `Disallow: /x`, agent `a` is allowed `/x` in 2.x; 1.x let `b` join `a`'s group and disallowed `/x` for both.
* Lines that can't be used are reported in `ParseErrors` instead of being dropped silently. Parsing still never throws on bad content.
* `Allowed(Uri, string)` throws `ArgumentNullException` for a null Url or user agent.
* `UserAgents` and `BrowserUserAgents` are static classes: they only hold constants. `BrowserUserAgents.AllAgents` still works, but the classes can no longer be created or derived from.

## Thread safety

1.x was not safe to use from several threads while loading: queries during a load could throw `InvalidOperationException` or see half the rules. In 2.x, an instance can be queried from any number of threads, also while rules are being loaded or added; each query sees the rules from either before or after a load or add, never a mix. When several answers must come from the same load, read `Snapshot` once and query it. Build with `RobotsBuilder` on one thread and share the result once it is built.
