using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Robots
{
    public interface IRobots
    {
        Uri? BaseUri { get; set; }

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
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task LoadAsync(Stream stream, string baseUrl, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        void Load(Stream stream, Uri baseUri);

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task LoadAsync(Stream stream, Uri baseUri, CancellationToken cancellationToken = default(CancellationToken));

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
        /// Adds the rules in a robots.txt stream to the rules already loaded.
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="cancellationToken">Cancels the read.</param>
        Task AddAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken));

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

        int GetCrawlDelay();
        int GetCrawlDelay(string userAgent);

        IList<string> GetSitemapUrls();
    }
}