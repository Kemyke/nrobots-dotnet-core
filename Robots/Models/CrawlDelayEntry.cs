using System;

namespace Robots.Model
{
    public class CrawlDelayEntry : Entry
    {
        public CrawlDelayEntry()
            : base(EntryType.CrawlDelay)
        {}

        /// <summary>The exact delay, fractions included: "Crawl-delay: 1.5" is one and a half seconds.</summary>
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// The delay in whole seconds, rounded up so a crawler never waits less than the site asked: 1.5 gives 2.
        /// Setting it sets <see cref="Delay"/>.
        /// </summary>
        public int CrawlDelay
        {
            get { return RobotsLine.ToWholeSecondsRoundedUp(Delay); }
            set { Delay = TimeSpan.FromSeconds(value); }
        }
    }
}
