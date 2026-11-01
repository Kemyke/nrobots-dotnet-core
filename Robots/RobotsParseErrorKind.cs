namespace Robots
{
    /// <summary>
    /// Why a robots.txt line was skipped.
    /// </summary>
    public enum RobotsParseErrorKind
    {
        /// <summary>A line with content but no ':' separating a directive from its value.</summary>
        MalformedLine,

        /// <summary>A directive other than User-agent, Allow, Disallow, Crawl-delay or Sitemap.</summary>
        UnknownDirective,

        /// <summary>
        /// A Crawl-delay that isn't a number of seconds from 0 up to what a TimeSpan can hold, such as "-1",
        /// "1,5", "NaN" or an empty value.
        /// </summary>
        InvalidCrawlDelay,

        /// <summary>
        /// An Allow, Disallow or Crawl-delay line before the first User-agent line. RFC 9309, section 2.2.2, says
        /// to ignore such rules.
        /// </summary>
        RuleOutsideGroup,

        /// <summary>A User-agent line without a value.</summary>
        EmptyUserAgent,

        /// <summary>
        /// The last line of a downloaded robots.txt, cut off part-way by the 500 KiB limit and therefore ignored.
        /// </summary>
        LineCutOff,
    }
}
