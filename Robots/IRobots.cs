using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Robots
{
    public interface IRobots
    {
        Uri? BaseUri { get; set; }

        /// <summary>
        /// Everything loaded, as one immutable snapshot, so several answers can come from the same load while another
        /// thread reloads.
        /// </summary>
        RobotsSnapshot Snapshot { get; }

        /// <summary>The groups as written, in file order.</summary>
        IReadOnlyList<RobotsGroup> Groups { get; }

        /// <summary>The Sitemap values, in file order.</summary>
        IReadOnlyList<string> Sitemaps { get; }

        /// <summary>The lines that were skipped, in the order they were read. Empty for a clean file.</summary>
        IReadOnlyList<RobotsParseError> ParseErrors { get; }

        /// <summary>
        /// Load the robots.txt from a web resource
        /// </summary>
        /// <param name="robotsUri">The Uri of the web resource</param>
        void Load(Uri robotsUri);

        /// <summary>
        /// Load the robots.txt from a web resource
        /// </summary>
        /// <param name="robotsUri">The Uri of the web resource</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        Task LoadAsync(Uri robotsUri, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        void Load(Stream stream, string baseUrl);

        /// <summary>
        /// Loads the robots.txt from a stream in the given encoding.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        void Load(Stream stream, string baseUrl, Encoding? encoding);

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task LoadAsync(Stream stream, string baseUrl, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Loads the robots.txt from a stream in the given encoding.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task LoadAsync(Stream stream, string baseUrl, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        void Load(Stream stream, Uri baseUri);

        /// <summary>
        /// Loads the robots.txt from a stream in the given encoding.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        void Load(Stream stream, Uri baseUri, Encoding? encoding);

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Loads the robots.txt from a stream in the given encoding.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task LoadAsync(Stream stream, Uri baseUri, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Loads the robots.txt from the specified string. 
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt to load. May not be null</param>
        /// <param name="baseUrl"></param>
        void LoadContent(string fileContent, string baseUrl);

        /// <summary>
        /// Loads the robots.txt from the specified string. 
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt to load. May not be null</param>
        /// <param name="baseUri"></param>
        void LoadContent(string fileContent, Uri baseUri);

        /// <summary>
        /// Loads the robots.txt from the specified TextReader. 
        /// </summary>
        /// <param name="reader">The TextReader used to feed the robots.txt data into the document. May not be null.</param>
        /// <param name="baseUri"></param>
        void Load(TextReader reader, Uri baseUri);

        /// <summary>
        /// Loads the robots.txt from the specified TextReader. 
        /// </summary>
        /// <param name="reader">The TextReader used to feed the robots.txt data into the document. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task LoadAsync(TextReader reader, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Adds the rules in a robots.txt string to the rules already loaded. Relative paths resolve
        /// against <see cref="BaseUri"/>, which stays unchanged.
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt rules to add. May not be null.</param>
        void AddContent(string fileContent);

        /// <summary>
        /// Adds the rules in a robots.txt stream to the rules already loaded.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        void Add(Stream stream);

        /// <summary>
        /// Adds the rules in a robots.txt stream in the given encoding to the rules already loaded.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        void Add(Stream stream, Encoding? encoding);

        /// <summary>
        /// Adds the rules in a robots.txt stream to the rules already loaded.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task AddAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Adds the rules in a robots.txt stream in the given encoding to the rules already loaded.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="encoding">The text encoding; null for UTF-8. A byte order mark in the stream takes precedence.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task AddAsync(Stream stream, Encoding? encoding, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Adds the rules from a TextReader to the rules already loaded.
        /// </summary>
        /// <param name="reader">The TextReader with the robots.txt rules to add. May not be null.</param>
        void Add(TextReader reader);

        /// <summary>
        /// Adds the rules from a TextReader to the rules already loaded.
        /// </summary>
        /// <param name="reader">The TextReader with the robots.txt rules to add. May not be null.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task AddAsync(TextReader reader, CancellationToken cancellationToken = default(CancellationToken));

        bool Allowed(string url);
        bool Allowed(string url, string userAgent);
        bool Allowed(Uri uri);
        bool Allowed(Uri uri, string userAgent);

        /// <summary>
        /// The group that applies to a crawler, combined from every group for its product token (or the "*" groups
        /// when none match), or null when none applies.
        /// </summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        RobotsGroup? GetGroup(string userAgent);

        /// <summary>The Crawl-delay for the "*" group in whole seconds, rounded up. 0 when none applies.</summary>
        int GetCrawlDelay();

        /// <summary>The Crawl-delay for a crawler in whole seconds, rounded up. 0 when none applies.</summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        int GetCrawlDelay(string userAgent);

        /// <summary>The exact Crawl-delay for the "*" group, fractions included.</summary>
        /// <param name="crawlDelay">The delay, or TimeSpan.Zero when the method returns false.</param>
        /// <returns>False when no group that applies has a valid Crawl-delay.</returns>
        bool TryGetCrawlDelay(out TimeSpan crawlDelay);

        /// <summary>The exact Crawl-delay for a crawler, fractions included.</summary>
        /// <param name="userAgent">The crawler's product token, or a User-Agent string that starts with it.</param>
        /// <param name="crawlDelay">The delay, or TimeSpan.Zero when the method returns false.</param>
        /// <returns>False when no group that applies has a valid Crawl-delay.</returns>
        bool TryGetCrawlDelay(string userAgent, out TimeSpan crawlDelay);

        IList<string> GetSitemapUrls();
    }
}