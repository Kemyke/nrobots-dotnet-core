using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Robots.Model;

namespace Robots
{
    public class Robots : IRobots
    {
        #region Private Fields

        private readonly List<Entry> _entries = new List<Entry>();

        private Uri _baseUri;

        private readonly HttpClient _httpClient;

        // Shared so repeated loads reuse connections instead of exhausting sockets.
        private static readonly HttpClient DefaultHttpClient = CreateDefaultHttpClient();

        #endregion

        #region Constructors

        public Robots()
            : this(DefaultHttpClient)
        {
        }

        /// <summary>
        /// Creates a Robots that loads web resources through the given HttpClient.
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
            get { return _baseUri; }
            set
            {
                if (value == _baseUri)
                    return;

                _baseUri = value;
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
            Load(new StreamReader(stream), baseUri);
        }

        /// <summary>
        /// Loads the robots.txt from a stream.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        public Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            return LoadAsync(new StreamReader(stream), baseUri, cancellationToken);
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
            LoadCoreAsync(reader, baseUri, false, CancellationToken.None).GetAwaiter().GetResult();
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

            return LoadCoreAsync(reader, baseUri, true, cancellationToken);
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
#if NET5_0_OR_GREATER
            using (var response = isAsync
                ? await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false)
                : _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
#else
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
#endif
            {
                response.EnsureSuccessStatusCode();

#if NET5_0_OR_GREATER
                using (var responseStream = isAsync
                    ? await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)
                    : response.Content.ReadAsStream(cancellationToken))
#else
                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
#endif
                using (var reader = new StreamReader(responseStream))
                {
                    // RequestUri is the final address after any redirects.
                    var finalUri = response.RequestMessage.RequestUri;
                    var baseUri = ParseBaseUrl(string.Concat(finalUri.Scheme, "://", finalUri.Host));
                    await LoadCoreAsync(reader, baseUri, isAsync, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task LoadCoreAsync(TextReader reader, Uri baseUri, bool isAsync, CancellationToken cancellationToken)
        {
            _baseUri = baseUri;

            List<UserAgentEntry> userAgentsGroup = new List<UserAgentEntry>();
            bool addedEntriesToUserAgent = false;
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
                    break;

                if (string.IsNullOrEmpty(line))
                    continue;

                Entry entry;
                if (!Entry.TryParse(_baseUri, line, out entry))
                    continue;

                if (entry.Type == EntryType.UserAgent)
                {
                    UserAgentEntry userAgentEntry = (UserAgentEntry)entry;
                    if (addedEntriesToUserAgent)
                    {
                        userAgentsGroup.Clear();
                        addedEntriesToUserAgent = false;
                    }
                    
                    UserAgentEntry foundUserAgentEntry = FindExplicitUserAgentEntry(userAgentEntry.UserAgent);
                    if (foundUserAgentEntry == null)
                    {
                        _entries.Add(userAgentEntry);
                        userAgentsGroup.Add(userAgentEntry);
                    }
                    else
                    {
                        userAgentsGroup.Add(foundUserAgentEntry);
                    }
                }
                else if (entry.Type == EntryType.Comment)
                {
                    _entries.Add(entry);
                }
                else if (entry.Type == EntryType.Sitemap)
                {
                    _entries.Add(entry);
                }
                else if (userAgentsGroup.Count > 0)
                {
                    foreach (UserAgentEntry userAgent in userAgentsGroup)
                    {
                        userAgent.AddEntry(entry);
                    }
                    addedEntriesToUserAgent = true;
                }
                else
                {
                    continue;
                }
            } while (true);

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
            if (_entries.Count == 0)
                return true;

            if (!uri.IsAbsoluteUri)
                uri = new Uri(_baseUri, uri);

            //if (!IsInternalToDomain(uri))
            //    return true;

            if (uri.LocalPath == "/robots.txt")
                return false;

            var userAgentEntry = FindUserAgentEntry(userAgent);
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

            var userAgentEntry = FindUserAgentEntry(userAgent);
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

            foreach(Entry sitemapEntry in _entries)
            {
                if ( (sitemapEntry is SitemapEntry) && (sitemapEntry != null) )
                    sitemapUrls.Add(((SitemapEntry)sitemapEntry).SitemapUrl);
            }
            
            return sitemapUrls;
        }

#endregion

#region Private methods

        private UserAgentEntry FindUserAgentEntry(string userAgent)
        {
            UserAgentEntry allAgentsEntry = null;
            foreach (var entry in _entries)
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

        private UserAgentEntry FindExplicitUserAgentEntry(string userAgent)
        {
            UserAgentEntry targetEntry = null;
            foreach (var entry in _entries)
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

            _entries.Add(entry);
        }

        //This was removed since the _baseUri could change if a.com redirects to b.com. Then what if b.com has links to a.com? With this activated it will always allow because it would consider it external.
        //We check the ParsedWebpageData.IsInternal before calling IsUrlAllowed to be sure it follows our classification of internal and not NRobots
        //private bool IsInternalToDomain(Uri uri)
        //{
        //    return new Link(uri.AbsoluteUri).IsInternalTo(_baseUri.AbsoluteUri);
        //}
    }
}