using NUnit.Framework;

namespace E7.Protobuf.Tests
{
    /// <summary>
    /// Covers the core (non-archive) surface of the proto save API: saving/loading the main file, the active
    /// slot lifecycle, the byte/base64 converters, and the single-slot backup.
    /// </summary>
    public class ProtoSaveManagerTests
    {
        static TestSaveManager M => TestSaveManager.Manager;

        [SetUp]
        public void SetUp() => TestSaveManager.FullReset();

        [TearDown]
        public void TearDown() => TestSaveManager.FullReset();

        [Test]
        public void Save_ThenLoadMain_RoundTripsThroughEncryptedFile()
        {
            M.ApplyToActive(new TestSave { Level = 3, PlayerName = "Zed", PlayTimeSeconds = 88.25 });
            TestSaveManager.Save();

            TestSave loaded = M.LoadMain();
            Assert.AreEqual(3, loaded.Level);
            Assert.AreEqual("Zed", loaded.PlayerName);
            Assert.AreEqual(88.25, loaded.PlayTimeSeconds, 1e-9);
        }

        [Test]
        public void LoadMain_WhenNoFile_ReturnsFreshSave()
        {
            TestSave loaded = M.LoadMain();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.Level);
            Assert.AreEqual(string.Empty, loaded.PlayerName);
        }

        [Test]
        public void Active_AutoLoadsFresh_WhenNothingSavedYet()
        {
            Assert.IsNotNull(TestSaveManager.Active);
            Assert.AreEqual(0, TestSaveManager.Active.Level);
        }

        [Test]
        public void ReloadActive_DiscardsUnsavedChanges()
        {
            M.ApplyToActive(new TestSave { Level = 1 });
            TestSaveManager.Save();

            M.ApplyToActive(new TestSave { Level = 999 }); // unsaved edit
            M.ReloadActive();
            Assert.AreEqual(1, TestSaveManager.Active.Level);
        }

        [Test]
        public void ResetActive_ClearsMemoryButNotDisk()
        {
            M.ApplyToActive(new TestSave { Level = 7 });
            TestSaveManager.Save();

            M.ResetActive();
            Assert.AreEqual(0, TestSaveManager.Active.Level, "in-memory slot is reset");
            Assert.AreEqual(7, M.LoadMain().Level, "the file on disk is untouched until the next Save()");
        }

        [Test]
        public void ToBytes_FromBytes_RoundTrip()
        {
            var save = new TestSave { Level = 11, PlayerName = "Bytes" };
            save.Inventory["k"] = 5;

            byte[] bytes = M.ToBytes(save);
            TestSave back = M.FromBytes(bytes);

            Assert.AreEqual(11, back.Level);
            Assert.AreEqual("Bytes", back.PlayerName);
            Assert.AreEqual(5, back.Inventory["k"]);
        }

        [Test]
        public void ToBase64_FromBase64_RoundTrip()
        {
            var save = new TestSave { Level = 21, PlayerName = "B64" };

            string b64 = M.ToBase64(save);
            TestSave back = M.FromBase64(b64);

            Assert.AreEqual(21, back.Level);
            Assert.AreEqual("B64", back.PlayerName);
        }

        [Test]
        public void BackupActive_RestoreFromBackup_RoundTrip()
        {
            M.ApplyToActive(new TestSave { Level = 55, PlayerName = "safety" });
            M.BackupActive();

            M.ApplyToActive(new TestSave()); // wipe the active memory
            M.RestoreFromBackup();

            Assert.AreEqual(55, TestSaveManager.Active.Level);
            Assert.AreEqual("safety", TestSaveManager.Active.PlayerName);
        }

        [Test]
        public void ApplyToActive_SetsTheActiveSlot()
        {
            var s = new TestSave { Level = 123 };
            M.ApplyToActive(s);
            Assert.AreSame(s, TestSaveManager.Active);
        }
    }
}
