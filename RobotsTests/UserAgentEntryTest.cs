using System.Linq;
using Robots.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RobotsTests
{
    
    
    /// <summary>
    ///This is a test class for UserAgentEntryTest and is intended
    ///to contain all UserAgentEntryTest Unit Tests
    ///</summary>
    [TestClass]
    public class UserAgentEntryTest
    {
        [TestMethod]
        public void EntryType_Test()
        {
            var target = new UserAgentEntry();
            Assert.AreEqual(EntryType.UserAgent, target.Type);
        }

        [TestMethod]
        public void Create_UserAgent_Test()
        {
            var target = Entry.CreateEntry(EntryType.UserAgent);
            Assert.AreEqual(EntryType.UserAgent, target.Type);
        }

        /// <summary>
        ///A test for Entries
        ///</summary>
        [TestMethod]
        public void EntriesTest()
        {
            var target = new UserAgentEntry();
            Assert.IsEmpty(target.Entries);
        }

        /// <summary>
        ///A test for DisallowEntries
        ///</summary>
        [TestMethod]
        public void DisallowEntriesTest()
        {
            var target = new UserAgentEntry();
            Assert.IsEmpty(target.DisallowEntries);
        }

        /// <summary>
        ///A test for AllowEntries
        ///</summary>
        [TestMethod]
        public void AllowEntriesTest()
        {
            var target = new UserAgentEntry();
            Assert.IsEmpty(target.AllowEntries);
        }

        [TestMethod]
        public void Add_comment_Test()
        {
            var target = new UserAgentEntry();
            Entry entry = new CommentEntry();
            target.AddEntry(entry);
            Assert.IsNotEmpty(target.Entries);
            Assert.IsEmpty(target.AllowEntries);
            Assert.IsEmpty(target.DisallowEntries);
        }

        [TestMethod]
        public void Add_allow_entry_Test()
        {
            var target = new UserAgentEntry();
            Entry entry = new AllowEntry();
            target.AddEntry(entry);
            Assert.IsNotEmpty(target.Entries);
            Assert.IsNotEmpty(target.AllowEntries);
            Assert.IsEmpty(target.DisallowEntries);
        }

        [TestMethod]
        public void Add_disallow_entry_Test()
        {
            var target = new UserAgentEntry();
            Entry entry = new CommentEntry();
            target.AddEntry(entry);
            Assert.IsNotEmpty(target.Entries);
            Assert.IsEmpty(target.AllowEntries);
            Assert.IsEmpty(target.DisallowEntries);
        }

    }
}
