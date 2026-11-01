using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// CA2234 (pass Uri instead of string) is disabled for this file on purpose: these tests call the string
// overloads, such as Allowed(string) and LoadContent(string, string), because they are the public API the
// README documents and callers use, so they need test coverage.
#pragma warning disable CA2234

namespace RobotsTests
{
    /// <summary>
    /// Tests that a load replaces the previous one as a whole, and that readers never see a partial load.
    /// </summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class RobotsStateTest
    {
        private const string FirstUrl = "http://first.test.com";
        private const string FirstContent = "User-agent: *\nDisallow: /first";
        private const string SecondUrl = "http://second.test.com";
        private const string SecondContent = "User-agent: *\nDisallow: /second\nSitemap: http://second.test.com/sitemap.xml";

        public TestContext TestContext { get; set; }

        private static Robots.Robots LoadFirst()
        {
            var robots = new Robots.Robots();
            robots.LoadContent(FirstContent, FirstUrl);
            return robots;
        }

        private static void AssertFirstLoaded(Robots.Robots robots)
        {
            Assert.AreEqual(new Uri(FirstUrl), robots.BaseUri);
            Assert.IsFalse(robots.Allowed("/first"));
            Assert.IsTrue(robots.Allowed("/second"));
            Assert.IsEmpty(robots.GetSitemapUrls());
        }

        private static void AssertSecondLoaded(Robots.Robots robots)
        {
            Assert.AreEqual(new Uri(SecondUrl), robots.BaseUri);
            Assert.IsTrue(robots.Allowed("/first"));
            Assert.IsFalse(robots.Allowed("/second"));
            Assert.HasCount(1, robots.GetSitemapUrls());
        }

        [TestMethod]
        public void Load_replaces_the_previous_load()
        {
            var robots = LoadFirst();

            robots.LoadContent(SecondContent, SecondUrl);

            AssertSecondLoaded(robots);
        }

        [TestMethod]
        public async Task LoadAsync_replaces_the_previous_load()
        {
            var robots = LoadFirst();

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(SecondContent)))
            {
                await robots.LoadAsync(stream, SecondUrl, TestContext.CancellationToken).ConfigureAwait(false);
            }

            AssertSecondLoaded(robots);
        }

        [TestMethod]
        public void Load_keeps_the_previous_load_visible_until_it_completes()
        {
            var robots = LoadFirst();
            int checkedLines = 0;

            using (var reader = new InterceptingTextReader(SecondContent, line =>
            {
                AssertFirstLoaded(robots);
                checkedLines++;
            }))
            {
                robots.Load(reader, new Uri(SecondUrl));
            }

            // Three lines plus the read that returns null.
            Assert.AreEqual(4, checkedLines);
            AssertSecondLoaded(robots);
        }

        [TestMethod]
        public async Task LoadAsync_keeps_the_previous_load_visible_until_it_completes()
        {
            var robots = LoadFirst();
            int checkedLines = 0;

            using (var reader = new InterceptingTextReader(SecondContent, line =>
            {
                AssertFirstLoaded(robots);
                checkedLines++;
            }))
            {
                await robots.LoadAsync(reader, new Uri(SecondUrl), TestContext.CancellationToken).ConfigureAwait(false);
            }

            Assert.AreEqual(4, checkedLines);
            AssertSecondLoaded(robots);
        }

        [TestMethod]
        public void A_failed_load_keeps_the_previous_load()
        {
            var robots = LoadFirst();

            using (var reader = new InterceptingTextReader(SecondContent, line =>
            {
                if (line == 2)
                    throw new IOException("Connection lost");
            }))
            {
                Assert.ThrowsExactly<IOException>(() => robots.Load(reader, new Uri(SecondUrl)));
            }

            AssertFirstLoaded(robots);
        }

        [TestMethod]
        public async Task A_cancelled_load_keeps_the_previous_load()
        {
            var robots = LoadFirst();

            using (var cancellation = new CancellationTokenSource())
            using (var reader = new InterceptingTextReader(SecondContent, line =>
            {
                if (line == 1)
                    cancellation.Cancel();
            }))
            {
                await Assert.ThrowsAsync<OperationCanceledException>(() => robots.LoadAsync(reader, new Uri(SecondUrl), cancellation.Token)).ConfigureAwait(false);
            }

            AssertFirstLoaded(robots);
        }

        [TestMethod]
        public void Load_leaves_the_callers_stream_open()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(FirstContent)))
            {
                new Robots.Robots().Load(stream, FirstUrl);

                Assert.IsTrue(stream.CanRead);
            }
        }

        [TestMethod]
        public async Task LoadAsync_leaves_the_callers_stream_open()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(FirstContent)))
            {
                await new Robots.Robots().LoadAsync(stream, FirstUrl, TestContext.CancellationToken).ConfigureAwait(false);

                Assert.IsTrue(stream.CanRead);
            }
        }

        [TestMethod]
        public void Setting_BaseUri_keeps_the_loaded_rules()
        {
            var robots = LoadFirst();

            robots.BaseUri = new Uri(SecondUrl);

            Assert.AreEqual(new Uri(SecondUrl), robots.BaseUri);
            Assert.IsFalse(robots.Allowed("/first"));
        }
    }
}
