using System.Diagnostics.CodeAnalysis;
using Robots.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace RobotsTests
{
    
    
    /// <summary>
    ///This is a test class for UrlEntryTest and is intended
    ///to contain all UrlEntryTest Unit Tests
    ///</summary>
    [TestClass]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class UrlEntryTest
    {
        /// <summary>
        ///A test for Url
        ///</summary>
        [TestMethod]
        public void UrlTest()
        {
            UrlEntry target = CreateUrlEntry();
            var expected = new Uri("http://www.microsoft.com");
            target.Url = expected;
            Uri actual = target.Url;
            Assert.AreEqual(expected, actual);
       }

        private static UrlEntry CreateUrlEntry()
        {
            UrlEntry target = new AllowEntry();
            return target;
        }

        /// <summary>
        ///A test for Inverted
        ///</summary>
        [TestMethod]
        public void InvertedTest()
        {
            UrlEntry target = CreateUrlEntry();
            const bool expected = true;
            target.Inverted = expected;
            bool actual = target.Inverted;
            Assert.AreEqual(expected, actual);
        }
    }
}
