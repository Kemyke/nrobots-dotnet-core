# Migrating from 1.x to 2.x

NRobotsCore 2.0 keeps every 1.x method signature, so most code keeps compiling unchanged. The changes are in which frameworks are supported, how downloads behave, and the `IRobots` interface. This guide lists what to check, with examples.

## Checklist

| If your code... | Then |
|---|---|
| Targets .NET Framework 4.0–4.6.1 or .NET Standard 1.x | [Move to a supported framework](#supported-frameworks) |
| Implements `IRobots` itself (including test fakes) | [Add the `LoadAsync` members](#irobots-has-new-members) |
| Catches `WebException` around `Load(Uri)` | [Catch `HttpRequestException` instead](#exceptions) |
| Treats a 404 or 5xx error from `Load(Uri)` as "no rules" or retries on it | [Check the new status handling](#http-status-codes-rfc-9309) |
| Calls `Load` more than once on the same instance | [Loads now replace earlier rules](#a-load-replaces-the-previous-rules) |
| Loads from a Url in a server or UI app | [Use `LoadAsync`](#prefer-loadasync) |
| Passes its own `HttpClient` with a custom handler and calls `Load(Uri)` | [Check the handler supports `Send`](#your-own-httpclient) |
| Filters or logs requests by User-Agent | [A User-Agent is now sent](#user-agent) |

## Supported frameworks

| 1.x | 2.x |
|---|---|
| `net40`, `net451` | `net462` (.NET Framework 4.6.2 and later) |
| `netstandard1.1` | `netstandard2.0` |
| | `net10.0` |

Projects on .NET Framework 4.0–4.6.1 or on platforms that only support .NET Standard 1.x must stay on 1.x or move to a supported framework.

The assembly version is now 2.0.0.0, and the assembly carries the package's authors and copyright instead of the old template values.

## IRobots has new members

`IRobots` gained four methods:

```csharp
Task LoadAsync(Uri robotsUri, CancellationToken cancellationToken = default(CancellationToken));
Task LoadAsync(Stream stream, string baseUrl, CancellationToken cancellationToken = default(CancellationToken));
Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken));
Task LoadAsync(TextReader reader, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken));
```

Code that only *calls* `IRobots` is unaffected. A class that *implements* it, such as a test fake, must add them:

```csharp
public class FakeRobots : IRobots
{
    // ... the 1.x members as before ...

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

## A load replaces the previous rules

In 1.x, loading into an instance that already held rules added the new rules to the old ones. In 2.x each load replaces them:

```csharp
var robots = new Robots.Robots();
robots.LoadContent("User-agent: *\nDisallow: /a", "https://site-a.example");
robots.LoadContent("User-agent: *\nDisallow: /b", "https://site-b.example");

robots.Allowed("/a"); // 1.x: false (rules from both files). 2.x: true (only the second file)
robots.Allowed("/b"); // false in both
```

To combine several files, join the content and load it once. To check several sites, use one instance per site.

A load that fails or is cancelled now keeps the previous rules, where 1.x could leave them half-loaded.

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

`new Robots()` uses one shared `HttpClient`. To use your own, for example from `IHttpClientFactory`, or to set a proxy or timeout, pass it in. `Robots` never disposes a client you pass:

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

## Thread safety

1.x was not safe to use from several threads while loading: queries during a load could throw `InvalidOperationException` or see half the rules. In 2.x, an instance can be queried from any number of threads, also while it reloads; each query sees either the old rules or the new ones. Build with `RobotsBuilder` on one thread and share the result once it is built.
