using Robots.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace RobotsTests
{
    
    
    /// <summary>
    ///This is a test class for CommentEntryTest and is intended
    ///to contain all CommentEntryTest Unit Tests
    ///</summary>
    [TestClass()]
    public class CommentEntryTest
    {
        [TestMethod]
        public void EntryType_Test()
        {
            var target = new CommentEntry();
            Assert.AreEqual(EntryType.Comment, target.Type);
        }

        [TestMethod]
        public void Create_CommentEntry_Test()
        {
            var target = Entry.CreateEntry(EntryType.Comment);
            Assert.AreEqual(EntryType.Comment, target.Type);
        }
    }
}
