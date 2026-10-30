using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Robots.Model;

namespace Robots
{
    [SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
        Justification = "The class and its namespace were both named Robots in 1.x; renaming either breaks every caller, so it waits for a major version that bundles other breaking renames.")]
    public class Robots : IRobots
    {
        #region Private Fields

        // The loaded rules and their base Uri. A load builds a new State and publishes it with one
        // reference swap, so readers on other threads always see one complete load, never a partial one.
        private State _state = State.Empty;

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

        #endregion

        #region Accessors

        public Uri? BaseUri
        {
            get { return Volatile.Read(ref _state).BaseUri; }
            set
            {
                // Retry if a load or another setter published a new State in the meantime.
                State current, updated;
                do
                {
                    current = Volatile.Read(ref _state);
                    if (value == current.BaseUri)
                        return;

                    updated = new State(value, current.Entries);
                }
                while (Interlocked.CompareExchange(ref _state, updated, current) != current);
            }
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
            Load(stream, ParseBaseUrl(baseUrl));
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(Stream stream, string baseUrl, CancellationToken cancellationToken = default(CancellationToken))
        {
            return LoadAsync(stream, ParseBaseUrl(baseUrl), cancellationToken);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        public void Load(Stream stream, Uri baseUri)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            LoadCoreAsync(stream, baseUri, false, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return LoadCoreAsync(stream, baseUri, true, cancellationToken);
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
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            AddCoreAsync(stream, RequireBaseUri(), false, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Adds the rules in a robots.txt stream to the rules already loaded. See <see cref="AddContent"/>.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        /// <exception cref="InvalidOperationException">Nothing has been loaded and <see cref="BaseUri"/> isn't set.</exception>
        public Task AddAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return AddCoreAsync(stream, RequireBaseUri(), true, cancellationToken);
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
            AddCoreAsync(reader, RequireBaseUri(), false, CancellationToken.None).GetAwaiter().GetResult();
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

            return AddCoreAsync(reader, RequireBaseUri(), true, cancellationToken);
        }

        private Uri RequireBaseUri()
        {
            var baseUri = Volatile.Read(ref _state).BaseUri;
            if (baseUri == null)
                throw new InvalidOperationException("Load a robots.txt or set BaseUri before adding rules.");

            return baseUri;
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
                        Volatile.Write(ref _state, new State(baseUri, new List<Entry>()));
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
                            await LoadCoreAsync(limitedStream, baseUri, isAsync, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private async Task LoadCoreAsync(Stream stream, Uri baseUri, bool isAsync, CancellationToken cancellationToken)
        {
            // leaveOpen: the caller owns the stream, so disposing the reader must not close it.
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
            {
                await LoadCoreAsync(reader, baseUri, isAsync, stream as LengthLimitedStream, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task LoadCoreAsync(TextReader reader, Uri baseUri, bool isAsync, LengthLimitedStream? limitedStream, CancellationToken cancellationToken)
        {
            // Parse fully before publishing: readers keep seeing the previous load until then,
            // and a failed or cancelled load leaves it unchanged.
            var entries = await ParseAsync(reader, baseUri, isAsync, limitedStream, cancellationToken).ConfigureAwait(false);

            // Replaces the previous load instead of adding to it.
            Volatile.Write(ref _state, new State(baseUri, entries));
        }

        private async Task AddCoreAsync(Stream stream, Uri baseUri, bool isAsync, CancellationToken cancellationToken)
        {
            // leaveOpen: the caller owns the stream, so disposing the reader must not close it.
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
            {
                await AddCoreAsync(reader, baseUri, isAsync, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task AddCoreAsync(TextReader reader, Uri baseUri, bool isAsync, CancellationToken cancellationToken)
        {
            var added = await ParseAsync(reader, baseUri, isAsync, null, cancellationToken).ConfigureAwait(false);

            // Merge into the rules published now, and retry if a load or another add published in the meantime.
            State current, updated;
            do
            {
                current = Volatile.Read(ref _state);
                updated = new State(current.BaseUri, Merge(current.Entries, added));
            }
            while (Interlocked.CompareExchange(ref _state, updated, current) != current);
        }

        [SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
            Justification = "With isAsync false this is the synchronous path behind Load and Add: ReadLine must stay synchronous so they never wait on a task.")]
        private static async Task<List<Entry>> ParseAsync(TextReader reader, Uri baseUri, bool isAsync, LengthLimitedStream? limitedStream, CancellationToken cancellationToken)
        {
            List<Entry> entries = new List<Entry>();
            List<UserAgentEntry> userAgentsGroup = new List<UserAgentEntry>();
            bool addedEntriesToUserAgent = false;

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
                    if (pendingLine != null && (limitedStream == null || !limitedStream.LimitReached))
                        AddLine(entries, userAgentsGroup, ref addedEntriesToUserAgent, baseUri, pendingLine);
                    break;
                }

                if (pendingLine != null)
                    AddLine(entries, userAgentsGroup, ref addedEntriesToUserAgent, baseUri, pendingLine);
                pendingLine = line;
            } while (true);

            return entries;
        }

        // Combines two parsed files the way 1.x did when loading twice into one instance: the added
        // file was parsed on its own, and a group for an agent that already had one got its rules
        // appended to that group. Merged groups are copies, so the published State is never modified.
        private static List<Entry> Merge(List<Entry> existing, List<Entry> added)
        {
            var merged = new List<Entry>(existing);
            foreach (var entry in added)
            {
                if (entry.Type == EntryType.UserAgent)
                {
                    var addedGroup = (UserAgentEntry)entry;
                    int index = merged.FindIndex(e => e.Type == EntryType.UserAgent
                        && string.Equals(((UserAgentEntry)e).UserAgent, addedGroup.UserAgent, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        merged[index] = Combine((UserAgentEntry)merged[index], addedGroup);
                        continue;
                    }
                }

                merged.Add(entry);
            }

            return merged;
        }

        private static UserAgentEntry Combine(UserAgentEntry existing, UserAgentEntry added)
        {
            var combined = new UserAgentEntry { UserAgent = existing.UserAgent, Comment = existing.Comment };
            foreach (var entry in existing.Entries)
                combined.AddEntry(entry);
            foreach (var entry in added.Entries)
                combined.AddEntry(entry);

            return combined;
        }

        private static void AddLine(List<Entry> entries, List<UserAgentEntry> userAgentsGroup, ref bool addedEntriesToUserAgent, Uri baseUri, string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            Entry? entry;
            if (!Entry.TryParse(baseUri, line, out entry))
                return;

            if (entry.Type == EntryType.UserAgent)
            {
                UserAgentEntry userAgentEntry = (UserAgentEntry)entry;
                if (addedEntriesToUserAgent)
                {
                    userAgentsGroup.Clear();
                    addedEntriesToUserAgent = false;
                }

                UserAgentEntry? foundUserAgentEntry = FindExplicitUserAgentEntry(entries, userAgentEntry.UserAgent);
                if (foundUserAgentEntry == null)
                {
                    entries.Add(userAgentEntry);
                    userAgentsGroup.Add(userAgentEntry);
                }
                else
                {
                    userAgentsGroup.Add(foundUserAgentEntry);
                }
            }
            else if (entry.Type == EntryType.Comment)
            {
                entries.Add(entry);
            }
            else if (entry.Type == EntryType.Sitemap)
            {
                entries.Add(entry);
            }
            else if (userAgentsGroup.Count > 0)
            {
                foreach (UserAgentEntry userAgent in userAgentsGroup)
                {
                    userAgent.AddEntry(entry);
                }
                addedEntriesToUserAgent = true;
            }
        }

#endregion

#region Methods: Allow

        public bool Allowed(string url)
        {
            return Allowed(new Uri(url, UriKind.RelativeOrAbsolute), UserAgents.AllAgents);
        }

        public bool Allowed(string url, string userAgent)
        {
            return Allowed(new Uri(url, UriKind.RelativeOrAbsolute), userAgent);
        }

        public bool Allowed(Uri uri)
        {
            return Allowed(uri, UserAgents.AllAgents);
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
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (userAgent == null)
                throw new ArgumentNullException(nameof(userAgent));

            // One snapshot for the whole check, so a concurrent load can't mix two files.
            var state = Volatile.Read(ref _state);
            if (state.Entries.Count == 0)
                return true;

            if (!uri.IsAbsoluteUri)
            {
                if (state.BaseUri == null)
                    throw new InvalidOperationException("Set BaseUri before checking a relative Url.");

                uri = new Uri(state.BaseUri, uri);
            }

            string path = RobotsPattern.NormalizePath(uri.PathAndQuery);

            // RFC 9309, section 2.2.2: the /robots.txt URI is implicitly allowed.
            if (path == "/robots.txt")
                return true;

            // The length of the longest matching Allow and Disallow pattern, or -1 when none matches.
            int longestAllow = -1;
            int longestDisallow = -1;
            foreach (var group in FindGroups(state.Entries, userAgent))
            {
                foreach (var rule in group.Entries)
                {
                    var urlEntry = rule as UrlEntry;
                    var pattern = urlEntry?.MatchPattern;
                    if (pattern == null)
                        continue;

                    bool isAllow = rule.Type == EntryType.Allow;
                    if (pattern.Length <= (isAllow ? longestAllow : longestDisallow))
                        continue;

                    if (!RobotsPattern.Matches(path, pattern))
                        continue;

                    if (isAllow)
                        longestAllow = pattern.Length;
                    else if (rule.Type == EntryType.Disallow)
                        longestDisallow = pattern.Length;
                }
            }

            return longestDisallow <= longestAllow;
        }

#endregion

#region Methods: Crawl-Delay

        public int GetCrawlDelay()
        {
            return GetCrawlDelay(UserAgents.AllAgents);
        }

        public int GetCrawlDelay(string userAgent)
        {
            foreach (var group in FindGroups(Volatile.Read(ref _state).Entries, userAgent))
            {
                if (group.CrawlDelayEntry != null)
                    return group.CrawlDelayEntry.CrawlDelay;
            }

            return 0;
        }

#endregion

#region Sitemap

        public IList<string> GetSitemapUrls()
        {
            List<string> sitemapUrls = new List<string>();

            foreach(Entry sitemapEntry in Volatile.Read(ref _state).Entries)
            {
                if ( (sitemapEntry is SitemapEntry) && (sitemapEntry != null) )
                    sitemapUrls.Add(((SitemapEntry)sitemapEntry).SitemapUrl);
            }

            return sitemapUrls;
        }

#endregion

#region Private methods

        // RFC 9309, section 2.2.1: every group whose product token matches the crawler's, case-insensitively;
        // when none does, every "*" group. Groups for the same token are combined.
        private static List<UserAgentEntry> FindGroups(List<Entry> entries, string userAgent)
        {
            string token = RobotsPattern.IsGlobalAgent(userAgent) ? string.Empty : RobotsPattern.ProductToken(userAgent);

            var specific = new List<UserAgentEntry>();
            var global = new List<UserAgentEntry>();
            foreach (var entry in entries)
            {
                var group = entry as UserAgentEntry;
                if (group == null || group.Type != EntryType.UserAgent)
                    continue;

                if (RobotsPattern.IsGlobalAgent(group.UserAgent))
                    global.Add(group);
                else if (token.Length > 0 && string.Equals(RobotsPattern.ProductToken(group.UserAgent), token, StringComparison.OrdinalIgnoreCase))
                    specific.Add(group);
            }

            return specific.Count > 0 ? specific : global;
        }

        private static UserAgentEntry? FindExplicitUserAgentEntry(List<Entry> entries, string userAgent)
        {
            UserAgentEntry? targetEntry = null;
            foreach (var entry in entries)
            {
                var userAgentEntry = entry as UserAgentEntry;
                if (userAgentEntry == null || userAgentEntry.Type != EntryType.UserAgent)
                    continue;

                if (string.Equals(userAgentEntry.UserAgent, userAgent, StringComparison.OrdinalIgnoreCase))
                {
                    targetEntry = userAgentEntry;
                    break;
                }
            }

            return targetEntry;
        }

#endregion

        internal void AddEntry(Entry entry)
        {
            if (entry ==  null)
                throw new ArgumentNullException(nameof(entry));
            if (entry.Type != EntryType.Comment && entry.Type != EntryType.UserAgent)
                throw new ArgumentException("Only Comments and User-Agent entries can be added", nameof(entry));

            // Copy instead of adding to the published list, which readers may be enumerating.
            State current, updated;
            do
            {
                current = Volatile.Read(ref _state);
                var entries = new List<Entry>(current.Entries) { entry };
                updated = new State(current.BaseUri, entries);
            }
            while (Interlocked.CompareExchange(ref _state, updated, current) != current);
        }

        /// <summary>
        /// A loaded robots.txt. Never modified once published; changes publish a new State.
        /// </summary>
        private sealed class State
        {
            public static readonly State Empty = new State(null, new List<Entry>());

            public State(Uri? baseUri, List<Entry> entries)
            {
                BaseUri = baseUri;
                Entries = entries;
            }

            public Uri? BaseUri { get; }

            public List<Entry> Entries { get; }
        }

        //This was removed since the _baseUri could change if a.com redirects to b.com. Then what if b.com has links to a.com? With this activated it will always allow because it would consider it external.
        //We check the ParsedWebpageData.IsInternal before calling IsUrlAllowed to be sure it follows our classification of internal and not NRobots
        //private bool IsInternalToDomain(Uri uri)
        //{
        //    return new Link(uri.AbsoluteUri).IsInternalTo(_baseUri.AbsoluteUri);
        //}
    }
}