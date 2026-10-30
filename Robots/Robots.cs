using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Robots.Model;

namespace Robots
{
    public class Robots : IRobots
    {
        #region Private Fields

        // The loaded rules and their base Uri. A load builds a new State and publishes it with one
        // reference swap, so readers on other threads always see one complete load, never a partial one.
        private State _state = State.Empty;

        private readonly HttpClient _httpClient;

        private string _userAgent;

        // Shared so repeated loads reuse connections instead of exhausting sockets.
        private static readonly HttpClient DefaultHttpClient = CreateDefaultHttpClient();

        private static readonly string DefaultUserAgent = "NRobotsCore/" + typeof(Robots).Assembly.GetName().Version.ToString(3);

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
                throw new ArgumentNullException("httpClient");

            _httpClient = httpClient;
        }

        private static HttpClient CreateDefaultHttpClient()
        {
#if NET5_0_OR_GREATER
            // Recycle pooled connections so a long-lived client picks up DNS changes.
            return new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
#else
            return new HttpClient();
#endif
        }

        #endregion

        #region Accessors

        public Uri BaseUri
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
        public string UserAgent
        {
            get { return Volatile.Read(ref _userAgent); }
            set
            {
                if (value != null && !IsValidUserAgent(value))
                    throw new ArgumentException("Not a valid User-Agent header value.", "value");

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
                throw new ArgumentNullException("stream");

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
                throw new ArgumentNullException("stream");

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
                throw new ArgumentNullException("fileContent");

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
                throw new ArgumentNullException("reader");

            // Completes synchronously on every target: the sync path only calls TextReader.ReadLine.
            LoadCoreAsync(reader, baseUri, false, CancellationToken.None, null).GetAwaiter().GetResult();
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
                throw new ArgumentNullException("reader");

            return LoadCoreAsync(reader, baseUri, true, cancellationToken, null);
        }

        private static Uri ParseBaseUrl(string baseUrl)
        {
            Uri baseUri;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
                throw new ArgumentException("Invalid Url", "baseUrl");

            return baseUri;
        }

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
                    var finalUri = (response.RequestMessage ?? request).RequestUri;
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
                            await LoadCoreAsync(reader, baseUri, false, cancellationToken, null).ConfigureAwait(false);
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
                        var limitedStream = new LengthLimitedStream(responseStream, MaxDownloadBytes);
                        await LoadCoreAsync(limitedStream, baseUri, isAsync, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task LoadCoreAsync(Stream stream, Uri baseUri, bool isAsync, CancellationToken cancellationToken)
        {
            // leaveOpen: the caller owns the stream, so disposing the reader must not close it.
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
            {
                await LoadCoreAsync(reader, baseUri, isAsync, cancellationToken, stream as LengthLimitedStream).ConfigureAwait(false);
            }
        }

        private async Task LoadCoreAsync(TextReader reader, Uri baseUri, bool isAsync, CancellationToken cancellationToken, LengthLimitedStream limitedStream)
        {
            // Parse into a local list and publish it only once the whole file has been read: readers keep
            // seeing the previous load until then, and a failed or cancelled load leaves it unchanged.
            List<Entry> entries = new List<Entry>();
            List<UserAgentEntry> userAgentsGroup = new List<UserAgentEntry>();
            bool addedEntriesToUserAgent = false;

            // Each line is parsed only once the next one has been read, so the last line can be
            // dropped when the size limit cut it off part-way.
            string pendingLine = null;
            do
            {
                string line;
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

            // Replaces the previous load instead of adding to it.
            Volatile.Write(ref _state, new State(baseUri, entries));
        }

        private static void AddLine(List<Entry> entries, List<UserAgentEntry> userAgentsGroup, ref bool addedEntriesToUserAgent, Uri baseUri, string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            Entry entry;
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

                UserAgentEntry foundUserAgentEntry = FindExplicitUserAgentEntry(entries, userAgentEntry.UserAgent);
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

        public bool Allowed(Uri uri, string userAgent)
        {
            // One snapshot for the whole check, so a concurrent load can't mix two files.
            var state = Volatile.Read(ref _state);
            if (state.Entries.Count == 0)
                return true;

            if (!uri.IsAbsoluteUri)
                uri = new Uri(state.BaseUri, uri);

            //if (!IsInternalToDomain(uri))
            //    return true;

            if (uri.LocalPath == "/robots.txt")
                return false;

            var userAgentEntry = FindUserAgentEntry(state.Entries, userAgent);
            if (userAgentEntry == null)
                return true;


            string[] uriParts = uri.PathAndQuery.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var disallowEntry in userAgentEntry.DisallowEntries)
            {
                bool result;
                if (CheckDisallowedEntry(disallowEntry, uriParts, out result))
                {
                    if (CheckExplicitlyAllowed(userAgentEntry, uri))
                        return true;

                    return result;
                }
            }

            return true;
        }

#endregion

#region Methods: Crawl-Delay

        public int GetCrawlDelay()
        {
            return GetCrawlDelay(UserAgents.AllAgents);
        }

        public int GetCrawlDelay(string userAgent)
        {
            int crawlDelay = 0;

            var userAgentEntry = FindUserAgentEntry(Volatile.Read(ref _state).Entries, userAgent);
            if (userAgentEntry == null)
                return crawlDelay;

            if (userAgentEntry.CrawlDelayEntry != null)
            {
                crawlDelay = userAgentEntry.CrawlDelayEntry.CrawlDelay;
            }

            return crawlDelay;
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

        private static UserAgentEntry FindUserAgentEntry(List<Entry> entries, string userAgent)
        {
            UserAgentEntry allAgentsEntry = null;
            foreach (var entry in entries)
            {
                var userAgentEntry = entry as UserAgentEntry;
                if (userAgentEntry == null || userAgentEntry.Type != EntryType.UserAgent)
                    continue;

                if (string.Compare(userAgentEntry.UserAgent, userAgent, StringComparison.OrdinalIgnoreCase) == 0)
                    return userAgentEntry;
                if (string.Compare(userAgentEntry.UserAgent, UserAgents.AllAgents, StringComparison.OrdinalIgnoreCase) == 0)
                    allAgentsEntry = userAgentEntry;
            }

            return allAgentsEntry;
        }

        private static UserAgentEntry FindExplicitUserAgentEntry(List<Entry> entries, string userAgent)
        {
            UserAgentEntry targetEntry = null;
            foreach (var entry in entries)
            {
                var userAgentEntry = entry as UserAgentEntry;
                if (userAgentEntry == null || userAgentEntry.Type != EntryType.UserAgent)
                    continue;

                if (string.Compare(userAgentEntry.UserAgent, userAgent, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    targetEntry = userAgentEntry;
                    break;
                }
            }

            return targetEntry;
        }

        private bool CheckExplicitlyAllowed(UserAgentEntry userAgentEntry, Uri uri)
        {
            //if (!IsInternalToDomain(uri))
            //    return true;

            string[] uriParts = uri.PathAndQuery.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var allowEntry in userAgentEntry.AllowEntries)
            {
                bool result;
                if (CheckAllowedEntry(allowEntry, uriParts, out result))
                {
                    return result;
                }
            }

            return false;
        }

        private static bool CheckAllowedEntry(AllowEntry entry, string[] uriParts, out bool allow)
        {
            allow = true;
            string[] robotInstructionUriParts = entry.Url.PathAndQuery.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            if (robotInstructionUriParts.Length > uriParts.Length)
                return false;

            bool mismatch = false;
            for (int i = 0; i < Math.Min(robotInstructionUriParts.Length, uriParts.Length); i++)
            {
                if (string.Compare(uriParts[i], robotInstructionUriParts[i], StringComparison.OrdinalIgnoreCase) != 0)
                {
                    mismatch = true;
                    break;
                }
            }
            if (mismatch)
            {
                return false;
            }

            return true;
        }

        private static bool CheckDisallowedEntry(DisallowEntry entry, string[] uriParts, out bool allow)
        {
            allow = true;
            string[] robotInstructionUriParts = entry.Url.PathAndQuery.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            if (robotInstructionUriParts.Length > uriParts.Length)
                return false;

            bool lastPartIsComplete = entry.Url.PathAndQuery.EndsWith("/");

            int end = Math.Min(robotInstructionUriParts.Length, uriParts.Length);
            bool mismatch = false;
            for (int i = 0; i < end; i++)
            {
                int index1 = i, index2 = i;
                if (entry.Inverted)
                {
                    index1 = robotInstructionUriParts.Length - i - 1;
                    index2 = uriParts.Length - i - 1;
                }

                bool partIsComplete = i < end - 1 || lastPartIsComplete;

                if (IsMismatch(robotInstructionUriParts[index1], uriParts[index2], partIsComplete))
                {
                    mismatch = true;
                    break;
                }
            }
            if (!mismatch)
            {
                allow = false;
                return true;
            }

            return false;
        }

        private static bool IsMismatch(string regsiteredPart, string testedPart, bool partIsComplete)
        {
            if (string.Compare("*", regsiteredPart, StringComparison.OrdinalIgnoreCase) == 0)
                return false;
            if (string.Compare(testedPart, regsiteredPart, StringComparison.OrdinalIgnoreCase) == 0)
                return false;
            if (!partIsComplete && testedPart.StartsWith(regsiteredPart, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

#endregion

        internal void AddEntry(Entry entry)
        {
            if (entry ==  null)
                throw new ArgumentNullException("entry");
            if (entry.Type != EntryType.Comment && entry.Type != EntryType.UserAgent)
                throw new ArgumentException("Only Comments and User-Agent entries can be added", "entry");

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

            public State(Uri baseUri, List<Entry> entries)
            {
                BaseUri = baseUri;
                Entries = entries;
            }

            public Uri BaseUri { get; }

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