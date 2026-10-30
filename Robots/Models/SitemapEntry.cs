using System.Diagnostics.CodeAnalysis;

namespace Robots.Model
{
    public class SitemapEntry : Entry
    {
        public SitemapEntry()
            : base(EntryType.Sitemap)
        {}

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings",
            Justification = "The value is reported exactly as written in robots.txt, which may be relative or not a valid URI; a Uri would reject or rewrite it. Changing the type would also break 1.x callers.")]
        public string SitemapUrl { get; set; } = string.Empty;
    }
}
