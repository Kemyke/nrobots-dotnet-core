using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RobotsTests
{
    /// <summary>
    /// Tests for how loading over HTTP follows RFC 9309: status codes, the size limit and the User-Agent header.
    /// </summary>
    [TestClass]
    public class RobotsHttpTest
    {
        private const string SiteUrl = "http://www.test.com/";

        // RFC 9309 2.5: the library parses the first 500 KiB.
        private const int MaxDownloadBytes = 500 * 1024;

        public TestContext TestContext { get; set; }

        private static Robots.Robots LoadedWithPrivateDisallowed(HttpClient client)
        {
            var robots = new Robots.Robots(client);
            robots.LoadContent("User-agent: *\nDisallow: /private", SiteUrl);
            return robots;
        }

        [TestMethod]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.Gone)]
        public void Load_allows_everything_when_robots_txt_is_unavailable(HttpStatusCode status)
        {
            using (var client = new HttpClient(FakeHttpMessageHandler.Returning(status)))
            {
                var robots = LoadedWithPrivateDisallowed(client);

                robots.Load(new Uri(SiteUrl));

                Assert.IsTrue(robots.Allowed("/private"));
                Assert.IsTrue(robots.Allowed("/"));
                Assert.AreEqual(new Uri(SiteUrl), robots.BaseUri);
            }
        }

        [TestMethod]
        public async Task LoadAsync_allows_everything_when_robots_txt_is_unavailable()
        {
            using (var client = new HttpClient(FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound)))
            {
                var robots = LoadedWithPrivateDisallowed(client);

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken);

                Assert.IsTrue(robots.Allowed("/private"));
            }
        }

        [TestMethod]
        [DataRow(HttpStatusCode.InternalServerError)]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        public void Load_disallows_everything_when_robots_txt_is_unreachable(HttpStatusCode status)
        {
            using (var client = new HttpClient(FakeHttpMessageHandler.Returning(status)))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri(SiteUrl));

                Assert.IsFalse(robots.Allowed("/"));
                Assert.IsFalse(robots.Allowed("/public/page.html"));
                Assert.IsFalse(robots.Allowed("/public/page.html", "anybot"));
                Assert.AreEqual(new Uri(SiteUrl), robots.BaseUri);
            }
        }

        [TestMethod]
        public async Task LoadAsync_disallows_everything_when_robots_txt_is_unreachable()
        {
            using (var client = new HttpClient(FakeHttpMessageHandler.Returning(HttpStatusCode.ServiceUnavailable)))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken);

                Assert.IsFalse(robots.Allowed("/public/page.html"));
            }
        }

        [TestMethod]
        public async Task LoadAsync_ignores_content_after_the_size_limit()
        {
            // The limit falls inside "Disallow: /straddle": its first 12 bytes, "Disallow: /s", fit and the rest doesn't.
            const string header = "User-agent: *\nDisallow: /before\n";
            const string cutLine = "Disallow: /straddle\n";
            int fillerLength = MaxDownloadBytes - header.Length - 12;
            string content = header + Comment(fillerLength) + cutLine + "Disallow: /after\n";

            using (var client = new HttpClient(FakeHttpMessageHandler.Serving(content)))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken);

                Assert.IsFalse(robots.Allowed("/before"));
                // The cut-off "Disallow: /s" would block every path starting with /s; it is dropped instead.
                Assert.IsTrue(robots.Allowed("/something"));
                Assert.IsTrue(robots.Allowed("/after"));
            }
        }

        [TestMethod]
        public void Load_ignores_content_after_the_size_limit()
        {
            const string header = "User-agent: *\nDisallow: /before\n";
            int fillerLength = MaxDownloadBytes - header.Length - 12;
            string content = header + Comment(fillerLength) + "Disallow: /straddle\nDisallow: /after\n";

            using (var client = new HttpClient(FakeHttpMessageHandler.Serving(content)))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri(SiteUrl));

                Assert.IsFalse(robots.Allowed("/before"));
                Assert.IsTrue(robots.Allowed("/something"));
                Assert.IsTrue(robots.Allowed("/after"));
            }
        }

        [TestMethod]
        public async Task LoadAsync_keeps_the_last_line_of_a_file_exactly_at_the_size_limit()
        {
            const string header = "User-agent: *\n";
            const string lastLine = "Disallow: /last";
            string content = header + Comment(MaxDownloadBytes - header.Length - lastLine.Length) + lastLine;
            Assert.AreEqual(MaxDownloadBytes, Encoding.UTF8.GetByteCount(content));

            using (var client = new HttpClient(FakeHttpMessageHandler.Serving(content)))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken);

                Assert.IsFalse(robots.Allowed("/last"));
            }
        }

        [TestMethod]
        public void Default_client_sends_the_library_User_Agent()
        {
            var expected = "NRobotsCore/" + typeof(Robots.Robots).Assembly.GetName().Version.ToString(3);

            Assert.AreEqual(expected, new Robots.Robots().UserAgent);
        }

        [TestMethod]
        public async Task Own_client_sends_its_own_User_Agent()
        {
            var handler = FakeHttpMessageHandler.Serving("User-agent: *");
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ClientBot/3.1");
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken);

                Assert.IsNull(robots.UserAgent);
                Assert.AreEqual("ClientBot/3.1", handler.UserAgents[0]);
            }
        }

        [TestMethod]
        public async Task UserAgent_is_sent_and_overrides_the_clients_own()
        {
            var handler = FakeHttpMessageHandler.Serving("User-agent: *");
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ClientBot/3.1");
                var robots = new Robots.Robots(client) { UserAgent = "MyCrawler/1.0 (+https://example.com/crawler)" };

                await robots.LoadAsync(new Uri(SiteUrl), TestContext.CancellationToken);

                Assert.AreEqual("MyCrawler/1.0 (+https://example.com/crawler)", handler.UserAgents[0]);
            }
        }

        [TestMethod]
        public void UserAgent_set_to_null_sends_none()
        {
            var handler = FakeHttpMessageHandler.Serving("User-agent: *");
            using (var client = new HttpClient(handler))
            {
                var robots = new Robots.Robots(client) { UserAgent = "MyCrawler/1.0" };
                robots.UserAgent = null;

                robots.Load(new Uri(SiteUrl));

                Assert.AreEqual(string.Empty, handler.UserAgents[0]);
            }
        }

        [TestMethod]
        public void UserAgent_rejects_an_invalid_header_value()
        {
            var robots = new Robots.Robots();

            Assert.ThrowsExactly<ArgumentException>(() => robots.UserAgent = "MyCrawler/1.0 (unclosed");
        }

        private static string Comment(int length)
        {
            // One comment line of exactly length bytes, newline included.
            return "#" + new string('x', length - 2) + "\n";
        }
    }
}
