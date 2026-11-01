using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Robots;
using Robots.Model;

// CA2234 (pass Uri instead of string) is disabled for this file on purpose: these tests call the string
// overloads, such as LoadContent(string, string), because they are the public API the README documents and
// callers use, so they need test coverage.
#pragma warning disable CA2234

namespace RobotsTests
{
    /// <summary>
    /// Tests for Crawl-delay parsing: fractions, invalid values, rounding and the exact TimeSpan accessor.
    /// </summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class CrawlDelayTest
    {
        private const string SiteUrl = "https://www.example.com";

        private static Robots.Robots Load(string robotsTxt)
        {
            var robots = new Robots.Robots();
            robots.LoadContent(robotsTxt, SiteUrl);
            return robots;
        }

        [TestMethod]
        [DataRow("1.5", 15_000_000L, 2)]
        [DataRow("0.1", 1_000_000L, 1)]
        [DataRow("1e3", 10_000_000_000L, 1000)]
        [DataRow("0", 0L, 0)]
        [DataRow("5", 50_000_000L, 5)]
        [DataRow(" 2 ", 20_000_000L, 2)]
        [DataRow("5?", 50_000_000L, 5)]
        [DataRow("0.0001", 1_000L, 1)]
        public void Valid_values_give_the_exact_delay_and_whole_seconds_rounded_up(string value, long expectedTicks, int expectedSeconds)
        {
            var robots = Load("User-agent: *\nCrawl-delay: " + value);

            Assert.IsTrue(robots.TryGetCrawlDelay(out var crawlDelay));
            Assert.AreEqual(TimeSpan.FromTicks(expectedTicks), crawlDelay);
            Assert.AreEqual(expectedSeconds, robots.GetCrawlDelay());
            Assert.IsEmpty(robots.ParseErrors);
        }

        [TestMethod]
        [DataRow("1,5")]
        [DataRow("-1")]
        [DataRow("NaN")]
        [DataRow("Infinity")]
        [DataRow("1e400")]
        [DataRow("922337203686")]
        [DataRow("")]
        [DataRow("soon")]
        public void Invalid_values_give_no_delay_and_a_parse_error(string value)
        {
            string line = "Crawl-delay: " + value;
            var robots = Load("User-agent: *\n" + line);

            Assert.IsFalse(robots.TryGetCrawlDelay(out var crawlDelay));
            Assert.AreEqual(TimeSpan.Zero, crawlDelay);
            Assert.AreEqual(0, robots.GetCrawlDelay());
            Assert.HasCount(1, robots.ParseErrors);
            Assert.AreEqual(RobotsParseErrorKind.InvalidCrawlDelay, robots.ParseErrors[0].Kind);
            Assert.AreEqual(2, robots.ParseErrors[0].LineNumber);
            Assert.AreEqual(line, robots.ParseErrors[0].Line);
        }

        [TestMethod]
        public void The_largest_valid_delay_fits_and_whole_seconds_cap_at_int_MaxValue()
        {
            var robots = Load("User-agent: *\nCrawl-delay: 922337203685");

            // A double holds 922337203685 seconds in ticks to within about 1,000 ticks (0.1 ms), not to the exact tick.
            Assert.IsTrue(robots.TryGetCrawlDelay(out var crawlDelay));
            Assert.AreEqual(922_337_203_685d, crawlDelay.TotalSeconds, 0.001);
            Assert.AreEqual(int.MaxValue, robots.GetCrawlDelay());
        }

        [TestMethod]
        public void An_invalid_delay_no_longer_hides_a_valid_one_after_it()
        {
            // 1.x stored the invalid line as 0, and the group's first Crawl-delay won.
            var robots = Load("User-agent: *\nCrawl-delay: soon\nCrawl-delay: 3");

            Assert.AreEqual(3, robots.GetCrawlDelay());
        }

        [TestMethod]
        public void The_first_valid_delay_of_a_group_wins()
        {
            var robots = Load("User-agent: *\nCrawl-delay: 2\nCrawl-delay: 9");

            Assert.AreEqual(2, robots.GetCrawlDelay());
        }

        [TestMethod]
        public void A_specific_group_without_a_delay_does_not_fall_back_to_the_star_group()
        {
            var robots = Load("User-agent: *\nCrawl-delay: 20\n\nUser-agent: ExampleBot\nDisallow: /x");

            Assert.IsFalse(robots.TryGetCrawlDelay("ExampleBot", out _));
            Assert.AreEqual(0, robots.GetCrawlDelay("ExampleBot"));
            Assert.IsTrue(robots.TryGetCrawlDelay("OtherBot", out var other));
            Assert.AreEqual(TimeSpan.FromSeconds(20), other);
        }

        [TestMethod]
        public void The_delay_is_found_by_product_token()
        {
            var robots = Load("User-agent: ExampleBot\nCrawl-delay: 1.5");

            Assert.IsTrue(robots.TryGetCrawlDelay("ExampleBot/2.1 (+https://example.com/bot)", out var crawlDelay));
            Assert.AreEqual(TimeSpan.FromSeconds(1.5), crawlDelay);
            Assert.AreEqual(2, robots.GetCrawlDelay("examplebot"));
        }

        [TestMethod]
        public void No_delay_gives_false_and_zero()
        {
            var robots = Load("User-agent: *\nDisallow: /x");

            Assert.IsFalse(robots.TryGetCrawlDelay(out var crawlDelay));
            Assert.AreEqual(TimeSpan.Zero, crawlDelay);
            Assert.AreEqual(0, robots.GetCrawlDelay());
        }

        [TestMethod]
        public void CrawlDelayEntry_keeps_the_exact_delay_and_rounds_up_the_whole_seconds()
        {
            Assert.IsTrue(Entry.TryParse(new Uri(SiteUrl), "Crawl-delay: 1.5", out var entry));
            var crawlDelayEntry = (CrawlDelayEntry)entry;

            Assert.AreEqual(TimeSpan.FromSeconds(1.5), crawlDelayEntry.Delay);
            Assert.AreEqual(2, crawlDelayEntry.CrawlDelay);
        }

        [TestMethod]
        public void Setting_CrawlDelay_sets_Delay()
        {
            var entry = new CrawlDelayEntry { CrawlDelay = 7 };

            Assert.AreEqual(TimeSpan.FromSeconds(7), entry.Delay);
        }

        [TestMethod]
        public void CrawlDelay_of_the_largest_TimeSpan_caps_at_int_MaxValue()
        {
            var entry = new CrawlDelayEntry { Delay = TimeSpan.MaxValue };

            Assert.AreEqual(int.MaxValue, entry.CrawlDelay);
        }

        [TestMethod]
        public void Entry_TryParse_returns_false_for_an_invalid_delay()
        {
            Assert.IsFalse(Entry.TryParse(new Uri(SiteUrl), "Crawl-delay: -1", out var entry));
            Assert.IsNull(entry);
        }
    }
}
