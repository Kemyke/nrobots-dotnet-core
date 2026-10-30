using System;

namespace Robots.Model
{
    public abstract class UrlEntry : Entry
    {
        protected UrlEntry(EntryType type)
            : base(type)
        { }

        public bool Inverted { get; set; }

        public Uri? Url { get; set; }

        /// <summary>
        /// The rule's path pattern as written in robots.txt, normalized for matching. Null for entries created
        /// in code, which match on the path and query of <see cref="Url"/>.
        /// </summary>
        internal string? PathPattern { get; set; }

        internal string? MatchPattern
        {
            get { return PathPattern ?? (Url == null ? null : RobotsPattern.NormalizePattern(Url.PathAndQuery)); }
        }
    }
}
