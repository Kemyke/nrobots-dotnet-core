using System.Diagnostics.CodeAnalysis;
using Robots.Fluent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

// CA2234 (pass Uri instead of string) is disabled for this file on purpose: these tests call the string
// overloads, such as Allowed(string) and LoadContent(string, string), because they are the public API the
// README documents and callers use, so they need test coverage.
#pragma warning disable CA2234

namespace RobotsTests
{
    
    
    /// <summary>
    ///This is a test class for RobotsBuilderTest and is intended
    ///to contain all RobotsBuilderTest Unit Tests
    ///</summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class RobotsBuilderTest
    {
        /// <summary>
        ///A test for Allow
        ///</summary>
        [TestMethod]
        public void Building_robots_Test()
        {
            var baseUri = new Uri("http://www.microsoft.com");
            var robots = RobotsBuilder.Create(baseUri)
                .ForUserAgent("bot1")
                .Disallow("/web")
                .Disallow("/cloud")
                .Allow("/web/page1.aspx")

                .ForUserAgent("bot2")
                .Disallow("/")

                .ForUserAgent("*")
                .DisallowWithComment("/blocked", "with comment")

                .Robots;

            Assert.IsNotNull(robots);
            Assert.IsFalse(robots.Allowed("/web", "bot1"));
            Assert.IsFalse(robots.Allowed("/web/allowed", "bot1"));
            Assert.IsTrue(robots.Allowed("/web/page1.aspx", "bot1"));
            Assert.IsFalse(robots.Allowed("/", "bot2"));
            Assert.IsFalse(robots.Allowed("/page.aspx", "bot2"));
            Assert.IsFalse(robots.Allowed("/blocked"));
            Assert.IsFalse(robots.Allowed("/blocked", "*"));
            Assert.IsTrue(robots.Allowed("/notblocked"));
        }
    }
}
