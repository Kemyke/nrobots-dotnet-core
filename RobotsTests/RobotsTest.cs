using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

// CA2234 (pass Uri instead of string) is disabled for this file on purpose: these tests call the string
// overloads, such as Allowed(string) and LoadContent(string, string), because they are the public API the
// README documents and callers use, so they need test coverage.
#pragma warning disable CA2234

namespace RobotsTests
{
    /// <summary>
    ///This is a test class for RobotsTest and is intended
    ///to contain all RobotsTest Unit Tests
    ///</summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class RobotsTest
    {
        private const string BASE_URL = "http://www.test.com";

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        private static Robots.Robots RobotsContent
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent(
        @"User-Agent: *
Disallow: /disallowedfile.txt
Disallow: /disallowedfolder
Disallow: /disallowedfolder/subfolder
Disallow: /disallowedcompletefolder/
Allow: /allowedfile2.txt
Allow: /allowedfolder",
                        BASE_URL
                        );
                return robots;
            }
        }

        private async Task<Robots.Robots> LoadRobotsUrlAsync()
        {
            using (var handler = FakeHttpMessageHandler.Serving(
        @"User-agent: *
Disallow: /cashback/admin
Allow: /entertainment
Disallow: /account/"))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);
                await robots.LoadAsync(new Uri(BASE_URL), TestContext.CancellationToken).ConfigureAwait(false);
                return robots;
            }
        }

        private static Robots.Robots RobotsEmpty
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent("User-Agent: *\r\nDisallow: ", BASE_URL);
                return robots;
            }
        }

        private static Robots.Robots RobotsWildcards
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent(@"User-Agent: *
Disallow: */private/
Disallow: /a/*/b/
Disallow: /a/*$"
                                                 , BASE_URL);
                return robots;
            }
        }

        private static Robots.Robots RobotsCrawlDelay
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent(
        @"User-Agent: *
Crawl-Delay: 20

User-Agent: userAgentCrawlDelayIs1
Crawl-Delay: 1

User-Agent: userAgentCrawlDelayNotSpecified
Allow: /

User-Agent: userAgentCrawlDelayEmpty
Crawl-Delay: ",
                        BASE_URL
                        );
                return robots;
            }
        }

        private static Robots.Robots RobotsSitemap
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent(
@"Sitemap: http://a.com/sitemap.xml
",
                        BASE_URL
                        );
                return robots;
            }
        }

        private static Robots.Robots RobotsSitemapMultiple
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent(
@"Sitemap: http://a.com/sitemap.xml
Sitemap: http://b.com/sitemap.xml
Sitemap: http://c.com/sitemap.xml
",
                        BASE_URL
                        );
                return robots;
            }
        }

        private static Robots.Robots RobotsGroupedUserAgents
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent(
        @"User-Agent: msnbot
User-Agent: googlebot
Disallow: /aaa

User-Agent: slurp
User-Agent: blahblah
Disallow: /bbb
",
                        BASE_URL
                        );
                return robots;
            }
        }

        private static Robots.Robots RobotsContentWithQuerystringOnRoot
        {
            get
            {
                var robots = new Robots.Robots();
                robots.LoadContent(
        @"User-Agent: *
Disallow: /?category=whatever
Disallow: /?category=another&color=red
",
                        BASE_URL
                        );
                return robots;
            }
        }

        [TestMethod]
        public void Create_robots_with_content_Test()
        {
            var robots = RobotsContent;
        }

        [TestMethod]
        public void Create_robots_with_all_robots_out_content_Test()
        {
            var robots = RobotsEmpty;
        }

        [TestMethod]
        public async Task Create_robots_with_uri_Test()
        {
            var robots = await LoadRobotsUrlAsync().ConfigureAwait(false);
        }

        [TestMethod]
        public void Disallow_root_all_robots_out_content_Test()
        {
            bool actual = RobotsEmpty.Allowed(BASE_URL + "/");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Disallow_folder_all_robots_out_content_Test()
        {
            bool actual = RobotsEmpty.Allowed(BASE_URL + "/folder");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Disallow_file_all_robots_out_content_Test()
        {
            bool actual = RobotsEmpty.Allowed(BASE_URL + "/folder/file.txt");
            Assert.IsTrue(actual);
        }

        // 1.x disallowed this. RFC 9309 matches patterns character by character, so "*/private/" needs the
        // trailing slash, which "/folder/private" doesn't have.
        [TestMethod]
        public void Wildcard_pattern_with_trailing_slash_does_not_match_without_it_Test()
        {
            bool actual = RobotsWildcards.Allowed("/folder/private");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Disallow_subfolder_wildcard_content_Test()
        {
            bool actual = RobotsWildcards.Allowed("/a/private/b/");
            Assert.IsFalse(actual);
        }

        [TestMethod]
        public void Disallow_root_grouped_agents_robots_content_Test()
        {
            Assert.IsFalse(RobotsGroupedUserAgents.Allowed(BASE_URL + "/aaa", "googlebot"));
            Assert.IsFalse(RobotsGroupedUserAgents.Allowed(BASE_URL + "/aaa", "msnbot"));
            Assert.IsTrue(RobotsGroupedUserAgents.Allowed(BASE_URL + "/bbb", "googlebot"));
            Assert.IsTrue(RobotsGroupedUserAgents.Allowed(BASE_URL + "/bbb", "msnbot"));

            Assert.IsFalse(RobotsGroupedUserAgents.Allowed(BASE_URL + "/bbb", "slurp"));
            Assert.IsFalse(RobotsGroupedUserAgents.Allowed(BASE_URL + "/bbb", "blahblah"));
            Assert.IsTrue(RobotsGroupedUserAgents.Allowed(BASE_URL + "/aaa", "slurp"));
            Assert.IsTrue(RobotsGroupedUserAgents.Allowed(BASE_URL + "/aaa", "blahblah")); 
        }

        [TestMethod]
        public void Disallow_querystring_on_root_allows_root_Test()
        {
            Assert.IsTrue(RobotsContentWithQuerystringOnRoot.Allowed(BASE_URL, "googlebot"));
        }

        [TestMethod]
        public void Disallow_querystring_exact_match_not_supported_Test()
        {
            Assert.IsFalse(RobotsContentWithQuerystringOnRoot.Allowed(BASE_URL + "?category=whatever", "googlebot"));
        }

        [TestMethod]
        public void Disallow_querystring_nonexact_match_not_supported_Test()
        {
            Assert.IsTrue(RobotsContentWithQuerystringOnRoot.Allowed(BASE_URL + "?category=another&blah=blah", "googlebot"));
        }

        // 1.x allowed this. Under RFC 9309, "/a/*$" is "/a/" followed by anything up to the end, so it
        // disallows everything below /a/.
        [TestMethod]
        public void Wildcard_then_dollar_disallows_everything_below_Test()
        {
            bool actual = RobotsWildcards.Allowed("/a/public/c/");
            Assert.IsFalse(actual);
        }

        [TestMethod]
        public void Allow_QuestionMarkAfterSlashTest_DisallowOnlyWhenTheQuestionMarkIsRightAfterTheSlash()
        {
            var robots = new Robots.Robots();
            robots.LoadContent(
                @"User-Agent: *
Disallow: /?
Disallow: /disallowfolder/?private
Allow: /?public/public"
                ,BASE_URL
            );

            Assert.IsFalse(robots.Allowed("/?blablabla"));
            Assert.IsTrue(robots.Allowed("/blablabla/blabla"));
            Assert.IsTrue(robots.Allowed("/blablabla/?blabla"));
            Assert.IsTrue(robots.Allowed("/blab?labla"));
            Assert.IsFalse(robots.Allowed("/disallowfolder/?private"));
            Assert.IsTrue(robots.Allowed("/?public/public"));
        }

        [TestMethod]
        public void Explicit_disallowed_file_Test()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/disallowedfile.txt");
            Assert.IsFalse(actual);
        }

        [TestMethod]
        public void Implicit_allowed_file_Test()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/allowedfile.xml");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Explicit_disallowed_folder_Test()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/disallowedfolder");
            Assert.IsFalse(actual);
        }
        
        // 1.x disallowed this. RFC 9309 matches patterns character by character, so
        // "Disallow: /disallowedcompletefolder/" covers what is inside the folder, not the path without the slash.
        [TestMethod]
        public void Disallowed_folder_with_trailing_slash_does_not_cover_the_path_without_it_Test()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/disallowedcompletefolder");
            Assert.IsTrue(actual);
        }
        
        [TestMethod]
        public void Explicit_disallowed_complete_folder_Test_different_folder()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/disallowedcompletefolderwithmoretext");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Implicit_allowed_folder_Test()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/allowedfolder");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Explicit_allowed_file_Test()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/allowedfile2.txt");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Explicit_allowed_folder_Test()
        {
            bool actual = RobotsContent.Allowed(BASE_URL + "/allowedfolder2");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void Explicit_relative_disallowed_file_Test()
        {
            bool actual = RobotsContent.Allowed("/disallowedfile.txt");
            Assert.IsFalse(actual);
        }

        [TestMethod]
        public async Task Uri_loaded_disallow_subfolder_Test()
        {
            var robots = await LoadRobotsUrlAsync().ConfigureAwait(false);
            Assert.IsFalse(robots.Allowed("/cashback/admin"));
            Assert.IsFalse(robots.Allowed("/cashback/admin/aaa/tool.html"));
        }

        [TestMethod]
        public async Task Uri_loaded_allow_folder_Test()
        {
            var robots = await LoadRobotsUrlAsync().ConfigureAwait(false);
            bool actual = robots.Allowed("/entertainment");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public async Task Uri_loaded_allow_subfolder_Test()
        {
            var robots = await LoadRobotsUrlAsync().ConfigureAwait(false);
            bool actual = robots.Allowed("/entertainment/music");
            Assert.IsTrue(actual);
        }

        [TestMethod]
        public void CrawlDelay_NoUserAgentParam_UsesWildcardUserAgentCrawlDelayValue()
        {
            Assert.AreEqual(20, RobotsCrawlDelay.GetCrawlDelay());
        }

        [TestMethod]
        public void CrawlDelay_NoCrawlDelayValueInRobotsDotText_ReturnZero()
        {
            Assert.AreEqual(0, RobotsEmpty.GetCrawlDelay());
        }

        [TestMethod]
        public void CrawlDelay_NonExistentUserAgent_UsesWildcardCrawlDelayValue()
        {
            Assert.AreEqual(20, RobotsCrawlDelay.GetCrawlDelay("nonExistentUserAgent"));
        }

        [TestMethod]
        public void CrawlDelay_UserAgentCrawlDelayIs1_Returns1()
        {
            Assert.AreEqual(1, RobotsCrawlDelay.GetCrawlDelay("userAgentCrawlDelayIs1"));
        }

        [TestMethod]
        public void CrawlDelay_UserAgentCrawlDelayNotSpecified_ReturnsZero()
        {
            Assert.AreEqual(0, RobotsContent.GetCrawlDelay("userAgentCrawlDelayNotSpecified"));
        }

        [TestMethod]
        public void CrawlDelay_UserAgentCrawlDelayEmpty_ReturnsZero()
        {
            Assert.AreEqual(0, RobotsContent.GetCrawlDelay("userAgentCrawlDelayEmpty"));
        }

        [TestMethod]
        public void SitemapUrl_SingleSitemapPresent_ReturnsUrl()
        {
            Assert.AreEqual("http://a.com/sitemap.xml", RobotsSitemap.GetSitemapUrls()[0]);
        }

        [TestMethod]
        public void SitemapUrl_NoSpaceBetween_ReturnsUrl()
        {
            Robots.Robots robots = new Robots.Robots();
            robots.LoadContent(@"Sitemap:http://a.com/sitemap.xml", BASE_URL);

            Assert.AreEqual("http://a.com/sitemap.xml", robots.GetSitemapUrls()[0]);
        }

        [TestMethod]
        public void SitemapUrl_MultipleSpaceBetween_ReturnsUrl()
        {
            Robots.Robots robots = new Robots.Robots();
            robots.LoadContent(@"Sitemap:   http://a.com/sitemap.xml", BASE_URL);

            Assert.AreEqual("http://a.com/sitemap.xml", robots.GetSitemapUrls()[0]);
        }

        [TestMethod]
        public void SitemapUrl_NoUrl_Returns1EntryThatIsEmtpty()
        {
            Robots.Robots robots = new Robots.Robots();
            robots.LoadContent(@"Sitemap:  ", BASE_URL);

            Assert.AreEqual("", robots.GetSitemapUrls()[0]);
        }

        [TestMethod]
        public void SitemapUrl_KeywordCase_ReturnsUrl()
        {
            Robots.Robots robots = new Robots.Robots();
            robots.LoadContent(@"siteMAp: http://a.com/sitemap.xml", BASE_URL);

            Assert.AreEqual("http://a.com/sitemap.xml", robots.GetSitemapUrls()[0]);
        }

        [TestMethod]
        public void SitemapUrl_SitemapPresent_ReturnsUrls()
        {
            IList<string> result = RobotsSitemapMultiple.GetSitemapUrls();

            Assert.AreEqual("http://a.com/sitemap.xml", result[0]);
            Assert.AreEqual("http://b.com/sitemap.xml", result[1]);
            Assert.AreEqual("http://c.com/sitemap.xml", result[2]);
        }

        [TestMethod]
        public void Sitemap_NoSitemapValueInRobotsDotText_ReturnEmptyCollection()
        {
            Assert.IsEmpty(RobotsEmpty.GetSitemapUrls());
        }
    }
}
