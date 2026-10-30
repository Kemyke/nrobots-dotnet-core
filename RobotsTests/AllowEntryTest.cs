using Robots.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace RobotsTests
{
    /// <summary>
    ///This is a test class for AllowEntryTest and is intended
    ///to contain all AllowEntryTest Unit Tests
    ///</summary>
    [TestClass]
    public class AllowEntryTest
    {
        [TestMethod]
        public void EntryType_Test()
        {
            var target = new AllowEntry();
            Assert.AreEqual(EntryType.Allow, target.Type);
        }

        [TestMethod]
        public void Create_AllowEntry_Test()
        {
            var target = Entry.CreateEntry(EntryType.Allow);
            Assert.AreEqual(EntryType.Allow, target.Type);
        }

    }
}