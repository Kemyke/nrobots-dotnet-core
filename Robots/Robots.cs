using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Robots
{
    [SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
        Justification = "The class and its namespace were both named Robots in 1.x; renaming either breaks every caller, so it waits for a major version that bundles other breaking renames.")]
    public class Robots : IRobots
    {
        #region Private Fields

        // The loaded robots.txt. A load builds a new snapshot and publishes it with one reference swap, so readers on
        // other threads always see one complete load, never a partial one.
        private RobotsSnapshot _snapshot = RobotsSnapshot.Empty;

        private readonly HttpClient _httpClient;

        private string? _userAgent;

        // Shared so repeated loads reuse connections instead of exhausting sockets.
        private static readonly HttpClient DefaultHttpClient = CreateDefaultHttpClient();

        private static readonly string DefaultUserAgent = "NRobotsCore/" + (typeof(Robots).Assembly.GetName().Version ?? new Version(0, 0, 0)).ToString(3);

        // RFC 9309 2.5: crawlers must parse at least 500 KiB of a robots.txt and may ignore the rest.
        private const int MaxDownloadBytes = 500 * 1024;

        private const string DisallowAllContent = "User-agent: *\nDisallow: /";

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a Robots that loads web resources through a shared HttpClient, sending
        /// <see cref="UserAgent"/> "NRobotsCore/{version}" unless you set another.
        /// </summary>
        public Robots()
            : this(DefaultHttpClient)
        {
            _userAgent = DefaultUserAgent;
        }

        /// <summary>
        /// Creates a Robots that loads web resources through the given HttpClient. The client's own
        /// DefaultRequestHeaders, including its User-Agent, are used unless you set <see cref="UserAgent"/>.
        /// </summary>
        /// <param name="httpClient">The client used by Load(Uri) and LoadAsync. May not be null.</param>
        public Robots(HttpClient httpClient)
        {
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            _httpClient = httpClient;
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "The HttpClient owns the handler (disposeHandler: true), which CA2000 can't see; the client itself is the process-lifetime DefaultHttpClient.")]
        private static HttpClient CreateDefaultHttpClient()
        {
#if NET5_0_OR_GREATER
            // Recycle pooled connections so a long-lived client picks up DNS changes.
            return new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }, disposeHandler: true);
#else
            return new HttpClient();
#endif
        }

        /// <summary>
        /// Creates a Robots and loads a robots.txt string into it.
        /// </summary>
        /// <param name="fileContent">The robots.txt content. May not be null.</param>
        /// <param name="baseUri">The Url relative Urls are resolved against, such as "https://www.example.com".</param>
        public static Robots Parse(string fileContent, Uri baseUri)
        {
            var robots = new Robots();
            robots.LoadContent(fileContent, baseUri);
            return robots;
        }

        /// <summary>
        /// Creates a Robots and loads a robots.txt string into it.
        /// </summary>
        /// <param name="fileContent">The robots.txt content. May not be null.</param>
        /// <param name="baseUrl">The Url relative Urls are resolved against, such as "https://www.example.com".</param>
        public static Robots Parse(string fileContent, string baseUrl)
        {
            return Parse(fileContent, ParseBaseUrl(baseUrl));
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Everything loaded, as one immutable snapshot. Read it once and query the snapshot when several answers must
        /// come from the same load while another thread may reload this instance.
        /// </summary>
        public RobotsSnapshot Snapshot
        {
            get { return Volatile.Read(ref _snapshot); }
        }

        public Uri? BaseUri
        {
            get { return Snapshot.BaseUri; }
            set
            {
                // Retry if a load or another setter published a new snapshot in the meantime.
                RobotsSnapshot current, updated;
                do
                {
                    current = Volatile.Read(ref _snapshot);
                    if (value == current.BaseUri)
                        return;

                    updated = current.WithBaseUri(value);
                }
                while (Interlocked.CompareExchange(ref _snapshot, updated, current) != current);
            }
        }

        /// <summary>The groups as written, in file order. See <see cref="RobotsSnapshot.Groups"/>.</summary>
        public IReadOnlyList<RobotsGroup> Groups
        {
            get { return Snapshot.Groups; }
        }

        /// <summary>The Sitemap values, in file order.</summary>
        public IReadOnlyList<string> Sitemaps
        {
            get { return Snapshot.Sitemaps; }
        }

        /// <summary>The lines that were skipped. See <see cref="RobotsSnapshot.ParseErrors"/>.</summary>
        public IReadOnlyList<RobotsParseError> ParseErrors
        {
            get { return Snapshot.ParseErrors; }
        }

        /// <summary>
        /// The User-Agent header sent when loading robots.txt from a Url, for example
        /// "MyCrawler/1.0 (+https://example.com/crawler)". Defaults to "NRobotsCore/{version}" when the
        /// built-in HttpClient is used, and to null when you pass your own HttpClient, whose
        /// DefaultRequestHeaders then apply. Null sends no User-Agent from this class.
        /// </summary>
        /// <remarks>
        /// This only sets the HTTP header. To check rules for your crawler, pass its name to Allowed.
        /// </remarks>
        /// <exception cref="ArgumentException">The value is not a valid User-Agent header.</exception>
        public string? UserAgent
        {
            get { return Volatile.Read(ref _userAgent); }
            set
            {
                if (value != null && !IsValidUserAgent(value))
                    throw new ArgumentException("Not a valid User-Agent header value.", nameof(value));

                Volatile.Write(ref _userAgent, value);
            }
        }

        private static bool IsValidUserAgent(string value)
        {
            using (var request = new HttpRequestMessage())
            {
                return request.Headers.UserAgent.TryParseAdd(value);
            }
        }
        #endregion

        #region Load methods

        /// <summary>
        /// Load the robots.txt from a web resource
        /// </summary>
        /// <param name="robotsUri">The Uri of the web resource</param>
        /// <exception cref="HttpRequestException">The request failed or returned a non-success status code.</exception>
        public void Load(Uri robotsUri)
        {
            // Completes synchronously on .NET 5+ (HttpClient.Send). Older targets have no
            // synchronous HttpClient API, so this blocks until the request completes.
            LoadCoreAsync(robotsUri, false, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Load the robots.txt from a web resource
        /// </summary>
        /// <param name="robotsUri">The Uri of the web resource</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <exception cref="HttpRequestException">The request failed or returned a non-success status code.</exception>
        public Task LoadAsync(Uri robotsUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            return LoadCoreAsync(robotsUri, true, cancellationToken);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        public void Load(Stream stream, string baseUrl)
        {
            Load(stream, ParseBaseUrl(baseUrl), null);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        public void Load(Stream stream, string baseUrl, Encoding? encoding)
        {
            Load(stream, ParseBaseUrl(baseUrl), encoding);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(Stream stream, string baseUrl, CancellationToken cancellationToken = default(CancellationToken))
        {
            return LoadAsync(stream, ParseBaseUrl(baseUrl), null, cancellationToken);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(Stream stream, string baseUrl, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken))
        {
            return LoadAsync(stream, ParseBaseUrl(baseUrl), encoding, cancellationToken);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        public void Load(Stream stream, Uri baseUri)
        {
            Load(stream, baseUri, null);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        public void Load(Stream stream, Uri baseUri, Encoding? encoding)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            LoadCoreAsync(stream, baseUri, encoding, false, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            return LoadAsync(stream, baseUri, null, cancellationToken);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(Stream stream, Uri baseUri, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return LoadCoreAsync(stream, baseUri, encoding, true, cancellationToken);
        }

        /// <summary>
        /// Loads the robots.txt from the specified string.
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt to load. May not be null</param>
        /// <param name="baseUrl"></param>
        public void LoadContent(string fileContent, string baseUrl)
        {
            LoadContent(fileContent, ParseBaseUrl(baseUrl));
        }

        /// <summary>
        /// Loads the robots.txt from the specified string.
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt to load. May not be null</param>
        /// <param name="baseUri"></param>
        public void LoadContent(string fileContent, Uri baseUri)
        {
            if (fileContent == null)
                throw new ArgumentNullException(nameof(fileContent));

            using(var sr = new StringReader(fileContent))
            {
                Load(sr, baseUri);
            }
        }

        /// <summary>
        /// Loads the robots.txt from the specified TextReader.
        /// </summary>
        /// <param name="reader">The TextReader used to feed the robots.txt data into the document. May not be null.</param>
        /// <param name="baseUri"></param>
        public void Load(TextReader reader, Uri baseUri)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            // Completes synchronously on every target: the sync path only calls TextReader.ReadLine.
            LoadCoreAsync(reader, baseUri, false, null, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Loads the robots.txt from the specified TextReader.
        /// </summary>
        /// <param name="reader">The TextReader used to feed the robots.txt data into the document. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(TextReader reader, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            return LoadCoreAsync(reader, baseUri, true, null, cancellationToken);
        }

        /// <summary>
        /// Adds the rules in a robots.txt string to the rules already loaded, as 1.x did when loading twice
        /// into one instance. Relative paths resolve against <see cref="BaseUri"/>, which stays unchanged.
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt rules to add. May not be null.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public void AddContent(string fileContent)
        {
            if (fileContent == null)
                throw new ArgumentNullException(nameof(fileContent));

            using (var sr = new StringReader(fileContent))
            {
                Add(sr);
            }
        }

        /// <summary>
        /// Adds the rules in a robots.txt stream to the rules already loaded. See <see cref="AddContent"/>.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public void Add(Stream stream)
        {
            Add(stream, null);
        }

        /// <summary>
        /// Adds the rules in a robots.txt stream to the rules already loaded. See <see cref="AddContent"/>.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public void Add(Stream stream, Encoding? encoding)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            RequireBaseUri();
            AddCoreAsync(stream, encoding, false, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Adds the rules in a robots.txt stream to the rules already loaded. See <see cref="AddContent"/>.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public Task AddAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            return AddAsync(stream, null, cancellationToken);
        }

        /// <summary>
        /// Adds the rules in a robots.txt stream to the rules already loaded. See <see cref="AddContent"/>.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public Task AddAsync(Stream stream, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            RequireBaseUri();
            return AddCoreAsync(stream, encoding, true, cancellationToken);
        }

        /// <summary>
        /// Adds the rules from a TextReader to the rules already loaded. See <see cref="AddContent"/>.
        /// </summary>
        /// <param name="reader">The TextReader with the robots.txt rules to add. May not be null.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public void Add(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            // Completes synchronously on every target: the sync path only calls TextReader.ReadLine.
            RequireBaseUri();
            AddCoreAsync(reader, false, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Adds the rules from a TextReader to the rules already loaded. See <see cref="AddContent"/>.
        /// </summary>
        /// <param name="reader">The TextReader with the robots.txt rules to add. May not be null.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public Task AddAsync(TextReader reader, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            RequireBaseUri();
            return AddCoreAsync(reader, true, cancellationToken);
        }

        // Added rules keep the current BaseUri, so adding needs one.
        private void RequireBaseUri()
        {
            if (Snapshot.BaseUri == null)
                throw new InvalidOperationException("Load a robots.txt or set BaseUri before adding rules.");
        }

        private static Uri ParseBaseUrl(string baseUrl)
        {
            Uri? baseUri;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
                throw new ArgumentException("Invalid Url", nameof(baseUrl));

            return baseUri;
        }

        [SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
            Justification = "With isAsync false this is the synchronous path behind Load(Uri): HttpClient.Send and ReadAsStream must stay synchronous so Load never waits on a task.")]
        private async Task LoadCoreAsync(Uri robotsUri, bool isAsync, CancellationToken cancellationToken)
        {
            var uriBuilder = new UriBuilder(robotsUri) { Path = "/robots.txt" };

            // ResponseHeadersRead streams the body instead of buffering it, so it is read line by line below.
            using (var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri))
            {
                // Read once: another thread may change the property during the load.
                var userAgent = Volatile.Read(ref _userAgent);
                if (userAgent != null)
                    request.Headers.UserAgent.ParseAdd(userAgent);

#if NET5_0_OR_GREATER
                using (var response = isAsync
                    ? await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false)
                    : _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
#else
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
#endif
                {
                    // RequestUri is the final address after any redirects.
                    var finalUri = (response.RequestMessage ?? request).RequestUri ?? uriBuilder.Uri;
                    var baseUri = ParseBaseUrl(string.Concat(finalUri.Scheme, "://", finalUri.Host));

                    // RFC 9309 2.3.1.3: a 4xx means there is no robots.txt, so everything is allowed.
                    int status = (int)response.StatusCode;
                    if (status >= 400 && status < 500)
                    {
                        Volatile.Write(ref _snapshot, RobotsSnapshot.Empty.WithBaseUri(baseUri));
                        return;
                    }

                    // RFC 9309 2.3.1.4: a 5xx means the rules are unknown, so everything is disallowed.
                    if (status >= 500)
                    {
                        using (var reader = new StringReader(DisallowAllContent))
                        {
                            await LoadCoreAsync(reader, baseUri, false, null, cancellationToken).ConfigureAwait(false);
                        }
                        return;
                    }

                    // Anything else that isn't a success, such as a redirect the client didn't follow.
                    response.EnsureSuccessStatusCode();

#if NET5_0_OR_GREATER
                    using (var responseStream = isAsync
                        ? await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)
                        : response.Content.ReadAsStream(cancellationToken))
#else
                    using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
#endif
                    {
                        // RFC 9309 2.5: parse at most MaxDownloadBytes, so a huge or endless response can't exhaust memory.
                        using (var limitedStream = new LengthLimitedStream(responseStream, MaxDownloadBytes))
                        {
                            await LoadCoreAsync(limitedStream, baseUri, null, isAsync, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private async Task LoadCoreAsync(Stream stream, Uri baseUri, Encoding? encoding, bool isAsync, CancellationToken cancellationToken)
        {
            // leaveOpen: the caller owns the stream, so disposing the reader must not close it.
            using (var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, true, 1024, true))
            {
                await LoadCoreAsync(reader, baseUri, isAsync, stream as LengthLimitedStream, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task LoadCoreAsync(TextReader reader, Uri baseUri, bool isAsync, LengthLimitedStream? limitedStream, CancellationToken cancellationToken)
        {
            // Parse fully before publishing: readers keep seeing the previous load until then,
            // and a failed or cancelled load leaves it unchanged.
            var result = await ParseAsync(reader, isAsync, limitedStream, cancellationToken).ConfigureAwait(false);

            // Replaces the previous load instead of adding to it.
            Volatile.Write(ref _snapshot, new RobotsSnapshot(baseUri, result));
        }

        private async Task AddCoreAsync(Stream stream, Encoding? encoding, bool isAsync, CancellationToken cancellationToken)
        {
            // leaveOpen: the caller owns the stream, so disposing the reader must not close it.
            using (var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, true, 1024, true))
            {
                await AddCoreAsync(reader, isAsync, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task AddCoreAsync(TextReader reader, bool isAsync, CancellationToken cancellationToken)
        {
            var added = await ParseAsync(reader, isAsync, null, cancellationToken).ConfigureAwait(false);

            // Append to the snapshot published now, and retry if a load or another add published in the meantime.
            RobotsSnapshot current, updated;
            do
            {
                current = Volatile.Read(ref _snapshot);
                updated = current.Append(added);
            }
            while (Interlocked.CompareExchange(ref _snapshot, updated, current) != current);
        }

        [SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
            Justification = "With isAsync false this is the synchronous path behind Load and Add: ReadLine must stay synchronous so they never wait on a task.")]
        private static async Task<ParseResult> ParseAsync(TextReader reader, bool isAsync, LengthLimitedStream? limitedStream, CancellationToken cancellationToken)
        {
            var parser = new RobotsParser();

            // Each line is parsed only once the next one has been read, so the last line can be
            // dropped when the size limit cut it off part-way.
            string? pendingLine = null;
            do
            {
                string? line;
                if (isAsync)
                {
                    cancellationToken.ThrowIfCancellationRequested();
#if NET7_0_OR_GREATER
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#else
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
#endif
                }
                else
                {
                    line = reader.ReadLine();
                }

                if (line == null)
                {
                    if (pendingLine != null)
                    {
                        if (limitedStream == null || !limitedStream.LimitReached)
                            parser.AddLine(pendingLine);
                        else
                            parser.AddCutOffLine(pendingLine);
                    }
                    break;
                }

                if (pendingLine != null)
                    parser.AddLine(pendingLine);
                pendingLine = line;
            } while (true);

            return parser.Complete();
        }

        /// <summary>
        /// Publishes a snapshot built in code, by <see cref="Fluent.RobotsBuilder"/>.
        /// </summary>
        internal void Publish(RobotsSnapshot snapshot)
        {
            Volatile.Write(ref _snapshot, snapshot);
        }

#endregion

#region Methods: Allow

        /// <summary>Checks a Url for the "*" group. See <see cref="RobotsSnapshot.Allowed(Uri, string)"/>.</summary>
        public bool Allowed(string url)
        {
            return Snapshot.Allowed(RobotsSnapshot.ToUri(url), UserAgents.AllAgents);
        }

        /// <summary>Checks a Url for a crawler. See <see cref="RobotsSnapshot.Allowed(Uri, string)"/>.</summary>
        public bool Allowed(string url, string userAgent)
        {
            return Snapshot.Allowed(RobotsSnapshot.ToUri(url), userAgent);
        }

        /// <summary>Checks a Url for the "*" group. See <see cref="RobotsSnapshot.Allowed(Uri, string)"/>.</summary>
        public bool Allowed(Uri uri)
        {
            return Snapshot.Allowed(uri, UserAgents.AllAgents);
        }

        /// <summary>
        /// Checks whether a crawler may access a Url, following RFC 9309: the groups for the crawler's product
        /// token apply (or the "*" groups when none match), the longest matching rule wins, and Allow wins a tie.
        /// </summary>
        /// <param name="uri">The Url to check. A relative Url resolves against <see cref="BaseUri"/>.</param>
        /// <param name="userAgent">
        /// The crawler's product token, such as "ExampleBot", or a User-Agent string that starts with it, such as
        /// "ExampleBot/2.1 (+https://example.com/bot)". Matching is case-insensitive.
        /// </param>
        public bool Allowed(Uri uri, string userAgent)
        {
            return Snapshot.Allowed(uri, userAgent);
        }

        /// <summary>
        /// The group that applies to a crawler, or null when none does. See <see cref="RobotsSnapshot.GetGroup"/>.
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        public RobotsGroup? GetGroup(string userAgent)
        {
            return Snapshot.GetGroup(userAgent);
        }

#endregion

#region Methods: Crawl-Delay

        /// <summary>The Crawl-delay for the "*" group in whole seconds, rounded up. 0 when none applies.</summary>
        public int GetCrawlDelay()
        {
            return Snapshot.GetCrawlDelay();
        }

        /// <summary>
        /// The Crawl-delay for a crawler in whole seconds, rounded up so it never waits less than the site asked:
        /// "Crawl-delay: 1.5" gives 2. 0 when none applies. <see cref="TryGetCrawlDelay(string, out TimeSpan)"/> gives
        /// the exact value.
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        public int GetCrawlDelay(string userAgent)
        {
            return Snapshot.GetCrawlDelay(userAgent);
        }

        /// <summary>The exact Crawl-delay for the "*" group. See <see cref="TryGetCrawlDelay(string, out TimeSpan)"/>.</summary>
        public bool TryGetCrawlDelay(out TimeSpan crawlDelay)
        {
            return Snapshot.TryGetCrawlDelay(out crawlDelay);
        }

        /// <summary>
        /// The exact Crawl-delay for a crawler, fractions included: the first valid one of the groups that apply.
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        /// <param name="crawlDelay">The delay, or TimeSpan.Zero when the method returns false.</param>
        /// <returns>False when no group that applies has a valid Crawl-delay.</returns>
        public bool TryGetCrawlDelay(string userAgent, out TimeSpan crawlDelay)
        {
            return Snapshot.TryGetCrawlDelay(userAgent, out crawlDelay);
        }

#endregion

#region Sitemap

        /// <summary>The Sitemap values, in file order, as a new list.</summary>
        public IList<string> GetSitemapUrls()
        {
            return new List<string>(Snapshot.Sitemaps);
        }

#endregion

        //This was removed since the _baseUri could change if a.com redirects to b.com. Then what if b.com has links to a.com? With this activated it will always allow because it would consider it external.
        //We check the ParsedWebpageData.IsInternal before calling IsUrlAllowed to be sure it follows our classification of internal and not NRobots
        //private bool IsInternalToDomain(Uri uri)
        //{
        //    return new Link(uri.AbsoluteUri).IsInternalTo(_baseUri.AbsoluteUri);
        //}
    }
}
