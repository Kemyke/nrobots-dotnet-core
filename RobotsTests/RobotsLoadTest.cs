using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
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
    /// Tests for loading robots.txt over HTTP through Load(Uri) and LoadAsync.
    /// </summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class RobotsLoadTest
    {
        private const string Content = "User-agent: *\nDisallow: /private";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Load_requests_robots_txt_at_the_root()
        {
            using (var handler = FakeHttpMessageHandler.Serving(Content))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri("http://www.test.com/some/page.html"));

                Assert.AreEqual(new Uri("http://www.test.com/robots.txt"), handler.RequestedUris[0]);
                Assert.IsFalse(robots.Allowed("/private"));
                Assert.IsTrue(robots.Allowed("/public"));
            }
        }

        [TestMethod]
        public async Task LoadAsync_requests_robots_txt_at_the_root()
        {
            using (var handler = FakeHttpMessageHandler.Serving(Content))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri("http://www.test.com/some/page.html"), TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual(new Uri("http://www.test.com/robots.txt"), handler.RequestedUris[0]);
                Assert.IsFalse(robots.Allowed("/private"));
                Assert.IsTrue(robots.Allowed("/public"));
            }
        }

        [TestMethod]
        public void Load_uses_the_redirected_host_as_base()
        {
            using (var handler = FakeHttpMessageHandler.Serving(Content, new Uri("https://other.test.com/robots.txt")))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri("http://www.test.com/"));

                Assert.AreEqual(new Uri("https://other.test.com"), robots.BaseUri);
            }
        }

        [TestMethod]
        public async Task LoadAsync_uses_the_redirected_host_as_base()
        {
            using (var handler = FakeHttpMessageHandler.Serving(Content, new Uri("https://other.test.com/robots.txt")))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                await robots.LoadAsync(new Uri("http://www.test.com/"), TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual(new Uri("https://other.test.com"), robots.BaseUri);
            }
        }

        [TestMethod]
        public void Load_throws_on_a_redirect_the_client_did_not_follow()
        {
            using (var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Redirect))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                Assert.ThrowsExactly<HttpRequestException>(() => robots.Load(new Uri("http://www.test.com/")));
            }
        }

        [TestMethod]
        public async Task LoadAsync_throws_on_a_redirect_the_client_did_not_follow()
        {
            using (var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Redirect))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                await Assert.ThrowsExactlyAsync<HttpRequestException>(() => robots.LoadAsync(new Uri("http://www.test.com/"), TestContext.CancellationToken)).ConfigureAwait(false);
            }
        }

        [TestMethod]
        public async Task LoadAsync_honours_cancellation()
        {
            using (var handler = FakeHttpMessageHandler.Serving(Content))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);
                var cancelled = new CancellationToken(canceled: true);

                await Assert.ThrowsAsync<OperationCanceledException>(() => robots.LoadAsync(new Uri("http://www.test.com/"), cancelled)).ConfigureAwait(false);
                Assert.IsEmpty(handler.RequestedUris);
            }
        }

        [TestMethod]
        public void Constructor_rejects_a_null_client()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new Robots.Robots(null!));
        }

#if NET5_0_OR_GREATER
        [TestMethod]
        public void Load_sends_synchronously()
        {
            using (var handler = FakeHttpMessageHandler.Serving(Content))
            using (var client = new HttpClient(handler, disposeHandler: false))
            {
                var robots = new Robots.Robots(client);

                robots.Load(new Uri("http://www.test.com/"));

                Assert.AreEqual(1, handler.SyncSends);
                Assert.AreEqual(0, handler.AsyncSends);
            }
        }
#endif
    }
}
