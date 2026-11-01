using System.Diagnostics.CodeAnalysis;
using Robots.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace RobotsTests
{
    
    
    /// <summary>
    ///This is a test class for DisallowEntryTest and is intended
    ///to contain all DisallowEntryTest Unit Tests
    ///</summary>
    [TestClass()]
    [SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
        Justification = "MSTest discovers and creates test classes through reflection. Internal test classes need [DiscoverInternals] and then trip CA1812 (never instantiated) instead, because the analyzer can't see MSTest create them.")]
    public class DisallowEntryTest
    {
        [TestMethod]
        public void EntryType_Test()
        {
            var target = new DisallowEntry();
            Assert.AreEqual(EntryType.Disallow, target.Type);
        }

        [TestMethod]
        public void Create_DisallowEntry_Test()
        {
            var target = Entry.CreateEntry(EntryType.Disallow);
            Assert.AreEqual(EntryType.Disallow, target.Type);
        }

    }
}
