using Robots.Fluent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace RobotsTests
{
    
    
    /// <summary>
    ///This is a test class for RobotsBuilderTest and is intended
    ///to contain all RobotsBuilderTest Unit Tests
    ///</summary>
    [TestClass]
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
