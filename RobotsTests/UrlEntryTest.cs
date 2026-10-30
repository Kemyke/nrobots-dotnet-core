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
    public class UrlEntryTest
    {
        /// <summary>
        ///A test for Url
        ///</summary>
        [TestMethod]
        public void UrlTest()
        {
            UrlEntry target = CreateUrlEntry(); // TODO: Initialize to an appropriate value
            var expected = new Uri("http://www.microsoft.com");
            target.Url = expected;
            Uri actual = target.Url;
            Assert.AreEqual(expected, actual);
       }

        internal virtual UrlEntry CreateUrlEntry()
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
            UrlEntry target = CreateUrlEntry(); // TODO: Initialize to an appropriate value
            const bool expected = true;
            target.Inverted = expected;
            bool actual = target.Inverted;
            Assert.AreEqual(expected, actual);
        }
    }
}
