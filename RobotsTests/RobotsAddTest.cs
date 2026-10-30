using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RobotsTests
{
    /// <summary>
    /// Tests for AddContent, Add and AddAsync, which add rules to the loaded ones the way 1.x did when loading twice.
    /// </summary>
    [TestClass]
    public class RobotsAddTest
    {
        private const string SiteUrl = "http://www.test.com";
        private const string Loaded = "User-agent: *\nDisallow: /a\nCrawl-delay: 5";

        public TestContext TestContext { get; set; }

        private static Robots.Robots LoadedRobots()
        {
            var robots = new Robots.Robots();
            robots.LoadContent(Loaded, SiteUrl);
            return robots;
        }

        [TestMethod]
        public void AddContent_keeps_the_loaded_rules_and_adds_a_new_group()
        {
            var robots = LoadedRobots();

            robots.AddContent("User-agent: bot\nDisallow: /b");

            Assert.IsFalse(robots.Allowed("/a"));
            Assert.IsTrue(robots.Allowed("/b"));
            Assert.IsFalse(robots.Allowed("/b", "bot"));
        }

        [TestMethod]
        public void AddContent_appends_to_the_group_of_the_same_agent()
        {
            var robots = LoadedRobots();

            robots.AddContent("User-agent: *\nDisallow: /b\nCrawl-delay: 9");

            Assert.IsFalse(robots.Allowed("/a"));
            Assert.IsFalse(robots.Allowed("/b"));
            // As in 1.x, the group's first Crawl-delay wins.
            Assert.AreEqual(5, robots.GetCrawlDelay());
        }

        [TestMethod]
        public void AddContent_ignores_rules_before_the_first_User_agent_line()
        {
            var robots = LoadedRobots();

            // Joining "Disallow: /b" onto the loaded text would add it to the "*" group; 1.x and AddContent ignore it.
            robots.AddContent("Disallow: /b");

            Assert.IsTrue(robots.Allowed("/b"));
        }

        [TestMethod]
        public void AddContent_keeps_the_BaseUri_and_adds_sitemaps()
        {
            var robots = LoadedRobots();

            robots.AddContent("Sitemap: http://www.test.com/sitemap.xml");

            Assert.AreEqual(new Uri(SiteUrl), robots.BaseUri);
            Assert.AreEqual("http://www.test.com/sitemap.xml", robots.GetSitemapUrls()[0]);
        }

        [TestMethod]
        public void Load_after_AddContent_replaces_everything()
        {
            var robots = LoadedRobots();
            robots.AddContent("User-agent: *\nDisallow: /b");

            robots.LoadContent("User-agent: *\nDisallow: /c", SiteUrl);

            Assert.IsTrue(robots.Allowed("/a"));
            Assert.IsTrue(robots.Allowed("/b"));
            Assert.IsFalse(robots.Allowed("/c"));
        }

        [TestMethod]
        public void Add_stream()
        {
            var robots = LoadedRobots();

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("User-agent: *\nDisallow: /b")))
            {
                robots.Add(stream);

                Assert.IsTrue(stream.CanRead);
            }

            Assert.IsFalse(robots.Allowed("/a"));
            Assert.IsFalse(robots.Allowed("/b"));
        }

        [TestMethod]
        public async Task AddAsync_stream()
        {
            var robots = LoadedRobots();

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("User-agent: *\nDisallow: /b")))
            {
                await robots.AddAsync(stream, TestContext.CancellationToken);

                Assert.IsTrue(stream.CanRead);
            }

            Assert.IsFalse(robots.Allowed("/a"));
            Assert.IsFalse(robots.Allowed("/b"));
        }

        [TestMethod]
        public void Add_reader()
        {
            var robots = LoadedRobots();

            using (var reader = new StringReader("User-agent: *\nDisallow: /b"))
            {
                robots.Add(reader);
            }

            Assert.IsFalse(robots.Allowed("/a"));
            Assert.IsFalse(robots.Allowed("/b"));
        }

        [TestMethod]
        public async Task AddAsync_reader_keeps_the_loaded_rules_visible_until_it_completes()
        {
            var robots = LoadedRobots();
            int checkedLines = 0;

            using (var reader = new InterceptingTextReader("User-agent: *\nDisallow: /b", line =>
            {
                Assert.IsFalse(robots.Allowed("/a"));
                Assert.IsTrue(robots.Allowed("/b"));
                checkedLines++;
            }))
            {
                await robots.AddAsync(reader, TestContext.CancellationToken);
            }

            // Two lines plus the read that returns null.
            Assert.AreEqual(3, checkedLines);
            Assert.IsFalse(robots.Allowed("/b"));
        }

        [TestMethod]
        public async Task A_cancelled_AddAsync_keeps_the_loaded_rules()
        {
            var robots = LoadedRobots();

            using (var cancellation = new CancellationTokenSource())
            using (var reader = new InterceptingTextReader("User-agent: *\nDisallow: /b\nDisallow: /c", line =>
            {
                if (line == 2)
                    cancellation.Cancel();
            }))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(() => robots.AddAsync(reader, cancellation.Token));
            }

            Assert.IsFalse(robots.Allowed("/a"));
            Assert.IsTrue(robots.Allowed("/b"));
        }

        [TestMethod]
        public void AddContent_before_anything_is_loaded_throws()
        {
            var robots = new Robots.Robots();

            Assert.ThrowsExactly<InvalidOperationException>(() => robots.AddContent("User-agent: *\nDisallow: /b"));
        }

        [TestMethod]
        public void AddContent_after_setting_BaseUri_works()
        {
            var robots = new Robots.Robots { BaseUri = new Uri(SiteUrl) };

            robots.AddContent("User-agent: *\nDisallow: /b");

            Assert.IsFalse(robots.Allowed("/b"));
        }

        [TestMethod]
        public async Task Add_methods_reject_null()
        {
            var robots = LoadedRobots();

            Assert.ThrowsExactly<ArgumentNullException>(() => robots.AddContent(null));
            Assert.ThrowsExactly<ArgumentNullException>(() => robots.Add((Stream)null));
            Assert.ThrowsExactly<ArgumentNullException>(() => robots.Add((TextReader)null));
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => robots.AddAsync((Stream)null, TestContext.CancellationToken));
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => robots.AddAsync((TextReader)null, TestContext.CancellationToken));
        }
    }
}
