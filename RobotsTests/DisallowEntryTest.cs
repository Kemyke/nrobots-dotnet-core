using Robots.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace RobotsTests
{
    
    
    /// <summary>
    ///This is a test class for DisallowEntryTest and is intended
    ///to contain all DisallowEntryTest Unit Tests
    ///</summary>
    [TestClass()]
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
