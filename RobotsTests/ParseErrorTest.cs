using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Robots;

// CA2234 (pass Uri instead of string) is disabled for this file on purpose: these tests call the string
// overloads, such as LoadContent(string, string) and Allowed(string, string), because they are the public API
// the README documents and callers use, so they need test coverage.
#pragma warning disable CA2234

namespace RobotsTests
{
    /// <summary>
    /// Tests for ParseErrors: every kind, line numbers and raw lines, and how load, add and downloads update them.
    /// </summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class ParseErrorTest
    {
        private const string SiteUrl = "https://www.example.com";

        public TestContext TestContext { get; set; }

        private static Robots.Robots Load(string robotsTxt)
        {
            var robots = new Robots.Robots();
            robots.LoadContent(robotsTxt, SiteUrl);
            return robots;
        }

        private static string Describe(RobotsParseError error)
        {
            return error.LineNumber + " " + error.Kind + " " + error.Line;
        }

        [TestMethod]
        public void A_clean_file_has_no_errors()
        {
            var robots = Load("# comment\n\nUser-agent: *  # inline\nDisallow:\nAllow:\nDisallow: /x\nCrawl-delay: 1\nSitemap: https://www.example.com/s.xml\n");

            Assert.IsEmpty(robots.ParseErrors);
        }

        [TestMethod]
        public void Every_kind_is_reported_with_its_line_number_and_raw_line()
        {
            var robots = Load(
                "Disallow: /early\n" +
                "Crawl-delay: 5\n" +
                "User-agent: *\n" +
                "this line has no colon  # but a comment\n" +
                "Host: example.com\n" +
                "Crawl-delay: -1\n" +
                "User-agent:\n");

            Assert.AreEqual("1 RuleOutsideGroup Disallow: /early | 2 RuleOutsideGroup Crawl-delay: 5 | 4 MalformedLine this line has no colon  # but a comment | 5 UnknownDirective Host: example.com | 6 InvalidCrawlDelay Crawl-delay: -1 | 7 EmptyUserAgent User-agent:", string.Join(" | ", robots.ParseErrors.Select(Describe)));
        }

        [TestMethod]
        public void A_rule_before_the_first_group_is_ignored()
        {
            var robots = Load("Disallow: /early\nUser-agent: *\nDisallow: /x");

            Assert.IsTrue(robots.Allowed("/early"));
            Assert.IsFalse(robots.Allowed("/x"));
        }

        [TestMethod]
        public void An_empty_user_agent_starts_a_group_that_applies_to_no_crawler()
        {
            // As in Google's parser: /y belongs to the empty User-agent's group, not to a's.
            var robots = Load("User-agent: a\nDisallow: /x\nUser-agent:\nDisallow: /y");

            Assert.IsFalse(robots.Allowed("/x", "a"));
            Assert.IsTrue(robots.Allowed("/y", "a"));
            Assert.HasCount(1, robots.Groups);
        }

        [TestMethod]
        public void Bad_content_never_throws()
        {
            var robots = Load(":\n::::\n#\n\u0000\n﻿\nUser-agent\nDisallow /x\n%%%\n\r\n");

            Assert.IsTrue(robots.Allowed("/anything"));
            Assert.IsNotEmpty(robots.ParseErrors);
        }

        [TestMethod]
        public void A_load_replaces_the_errors()
        {
            var robots = Load("Host: a");

            robots.LoadContent("Host: b\nHost: c", SiteUrl);

            Assert.AreEqual("1 UnknownDirective Host: b | 2 UnknownDirective Host: c", string.Join(" | ", robots.ParseErrors.Select(Describe)));
        }

        [TestMethod]
        public void An_add_appends_its_errors_numbered_from_its_own_first_line()
        {
            var robots = Load("User-agent: *\nHost: a");

            robots.AddContent("Host: b");

            Assert.AreEqual("2 UnknownDirective Host: a | 1 UnknownDirective Host: b", string.Join(" | ", robots.ParseErrors.Select(Describe)));
        }

        [TestMethod]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        public async Task A_download_without_content_clears_the_errors(HttpStatusCode status)
        {
            using (var handler = FakeHttpMessageHandler.Returning(status))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);
                robots.LoadContent("Host: a", SiteUrl);

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken).ConfigureAwait(false);

                Assert.IsEmpty(robots.ParseErrors);
            }
        }

        [TestMethod]
        public async Task The_line_cut_off_by_the_size_limit_is_reported()
        {
            // The 500 KiB limit falls inside "Disallow: /straddle"; its first 12 bytes fit.
            const string header = "User-agent: *\n";
            int fillerLength = 500 * 1024 - header.Length - 12;
            string content = header + "#" + new string('x', fillerLength - 2) + "\n" + "Disallow: /straddle\n";

            using (var handler = FakeHttpMessageHandler.Serving(content))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken).ConfigureAwait(false);

                Assert.HasCount(1, robots.ParseErrors);
                Assert.AreEqual(RobotsParseErrorKind.LineCutOff, robots.ParseErrors[0].Kind);
                Assert.AreEqual(3, robots.ParseErrors[0].LineNumber);
                Assert.AreEqual("Disallow: /s", robots.ParseErrors[0].Line);
            }
        }

        [TestMethod]
        public void ToString_shows_line_kind_and_text()
        {
            var error = new RobotsParseError(3, "Host: example.com", RobotsParseErrorKind.UnknownDirective);

            Assert.AreEqual("Line 3 (UnknownDirective): Host: example.com", error.ToString());
        }

        [TestMethod]
        public void The_error_list_is_read_only()
        {
            var robots = Load("Host: a");

            var asList = (System.Collections.Generic.IList<RobotsParseError>)robots.ParseErrors;
            Assert.IsTrue(asList.IsReadOnly);
            Assert.ThrowsExactly<NotSupportedException>(() => asList.Clear());
        }
    }
}
