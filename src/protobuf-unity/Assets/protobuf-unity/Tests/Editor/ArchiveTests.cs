using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace E7.Protobuf.Tests
{
    /// <summary>
    /// Covers the rolling archive (versioned backup) API: <see cref="ProtoBinaryManager{PROTO, SELF}.ArchiveActive"/>,
    /// <see cref="ProtoBinaryManager{PROTO, SELF}.ListArchives"/>, <see cref="ProtoBinaryManager{PROTO, SELF}.RestoreFromArchive"/>,
    /// <see cref="ProtoBinaryManager{PROTO, SELF}.PruneArchives"/> and <see cref="ProtoBinaryManager{PROTO, SELF}.ClearArchives"/>.
    /// The <c>ListArchives</c> result is the main assertion surface.
    /// </summary>
    public class ArchiveTests
    {
        static TestSaveManager M => TestSaveManager.Manager;

        [SetUp]
        public void SetUp() => TestSaveManager.FullReset();

        [TearDown]
        public void TearDown() => TestSaveManager.FullReset();

        [Test]
        public void ArchiveActive_WritesOneListableDatedSnapshot()
        {
            TestSaveManager.SetClock(2026, 1, 1);
            M.ApplyToActive(new TestSave { Level = 5, PlayerName = "A" });

            ProtoArchiveInfo info = M.ArchiveActive();

            IReadOnlyList<ProtoArchiveInfo> list = M.ListArchives();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(new DateTime(2026, 1, 1), list[0].Date);
            Assert.AreEqual(info.FileName, list[0].FileName);
            // The file name carries the full day-month-year.
            StringAssert.Contains("2026-01-01", info.FileName);
            Assert.IsTrue(File.Exists(info.Path));
        }

        [Test]
        public void RepeatedArchiveWithinInterval_CollapsesToOne_AndKeepsLatestContent()
        {
            TestSaveManager.IntervalDays = 30;

            TestSaveManager.SetClock(2026, 1, 1);
            M.ApplyToActive(new TestSave { Level = 1 });
            M.ArchiveActive();

            TestSaveManager.SetClock(2026, 1, 15); // 14 days later, still < 30
            M.ApplyToActive(new TestSave { Level = 2 });
            M.ArchiveActive();

            IReadOnlyList<ProtoArchiveInfo> list = M.ListArchives();
            Assert.AreEqual(1, list.Count, "within one interval the snapshot is refreshed, not duplicated");
            Assert.AreEqual(new DateTime(2026, 1, 15), list[0].Date, "the surviving snapshot is re-dated to the latest write");

            M.RestoreFromArchive(0);
            Assert.AreEqual(2, TestSaveManager.Active.Level, "restored content is the latest within the interval");
        }

        [Test]
        public void ArchiveAcrossInterval_OpensNewSnapshot_AndFreezesTheOld()
        {
            TestSaveManager.IntervalDays = 30;

            TestSaveManager.SetClock(2026, 1, 1);
            M.ApplyToActive(new TestSave { Level = 10 });
            M.ArchiveActive();

            TestSaveManager.SetClock(2026, 2, 5); // 35 days later, >= 30
            M.ApplyToActive(new TestSave { Level = 20 });
            M.ArchiveActive();

            IReadOnlyList<ProtoArchiveInfo> list = M.ListArchives();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(new DateTime(2026, 2, 5), list[0].Date, "newest first");
            Assert.AreEqual(new DateTime(2026, 1, 1), list[1].Date);

            M.RestoreFromArchive(1);
            Assert.AreEqual(10, TestSaveManager.Active.Level, "the frozen older snapshot kept its own content");
            M.RestoreFromArchive(0);
            Assert.AreEqual(20, TestSaveManager.Active.Level);
        }

        [Test]
        public void Retention_AutoPrunesOldestBeyondCount()
        {
            TestSaveManager.IntervalDays = 30;
            TestSaveManager.RetentionCount = 3;

            DateTime start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 5; i++)
            {
                TestSaveManager.ClockOverride = start.AddDays(i * 31); // each call opens a fresh interval
                M.ApplyToActive(new TestSave { Level = i });
                M.ArchiveActive();
            }

            IReadOnlyList<ProtoArchiveInfo> list = M.ListArchives();
            Assert.AreEqual(3, list.Count, "kept only the retention count");
            Assert.AreEqual(start.AddDays(4 * 31).Date, list[0].Date, "newest kept");
            Assert.AreEqual(start.AddDays(2 * 31).Date, list[2].Date, "oldest kept is retention-count back");
        }

        [Test]
        public void RetentionZeroOrLess_NeverPrunes()
        {
            TestSaveManager.IntervalDays = 30;
            TestSaveManager.RetentionCount = 0; // unlimited

            DateTime start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 4; i++)
            {
                TestSaveManager.ClockOverride = start.AddDays(i * 31);
                M.ApplyToActive(new TestSave { Level = i });
                M.ArchiveActive();
            }

            Assert.AreEqual(4, M.ListArchives().Count);
        }

        [Test]
        public void PruneArchives_KeepsNewestN_AndReturnsDeletedCount()
        {
            TestSaveManager.IntervalDays = 30;
            TestSaveManager.RetentionCount = 0; // disable auto-prune; exercise the manual prune

            DateTime start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 5; i++)
            {
                TestSaveManager.ClockOverride = start.AddDays(i * 31);
                M.ApplyToActive(new TestSave { Level = i });
                M.ArchiveActive();
            }
            Assert.AreEqual(5, M.ListArchives().Count);

            int deleted = M.PruneArchives(2);
            Assert.AreEqual(3, deleted);

            IReadOnlyList<ProtoArchiveInfo> list = M.ListArchives();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(start.AddDays(4 * 31).Date, list[0].Date);
            Assert.AreEqual(start.AddDays(3 * 31).Date, list[1].Date);
        }

        [Test]
        public void RestoreFromArchive_OutOfRange_Throws()
        {
            TestSaveManager.SetClock(2026, 1, 1);
            M.ApplyToActive(new TestSave { Level = 1 });
            M.ArchiveActive();

            Assert.Throws<FileNotFoundException>(() => M.RestoreFromArchive(1));
            Assert.Throws<FileNotFoundException>(() => M.RestoreFromArchive(-1));
        }

        [Test]
        public void ClearArchives_RemovesAllArchives_ButLeavesSingleSlotBackupIntact()
        {
            // A single-slot backup...
            TestSaveManager.SetClock(2026, 1, 1);
            M.ApplyToActive(new TestSave { Level = 99, PlayerName = "backup" });
            M.BackupActive();

            // ...plus a couple of archives across intervals.
            M.ArchiveActive();
            TestSaveManager.SetClock(2026, 3, 1);
            M.ApplyToActive(new TestSave { Level = 7 });
            M.ArchiveActive();
            Assert.AreEqual(2, M.ListArchives().Count);

            int deleted = M.ClearArchives();
            Assert.AreEqual(2, deleted);
            Assert.AreEqual(0, M.ListArchives().Count);

            // The single-slot backup must still be restorable after clearing archives.
            M.ApplyToActive(new TestSave());
            M.RestoreFromBackup();
            Assert.AreEqual(99, TestSaveManager.Active.Level);
            Assert.AreEqual("backup", TestSaveManager.Active.PlayerName);
        }

        [Test]
        public void SingleSlotBackup_IsNotCountedAsAnArchive()
        {
            TestSaveManager.SetClock(2026, 1, 1);
            M.ApplyToActive(new TestSave { Level = 1, PlayerName = "archive-only" });
            M.ArchiveActive();

            // Writing the single-slot backup must not show up among the archives...
            M.ApplyToActive(new TestSave { Level = 500, PlayerName = "the-backup" });
            M.BackupActive();

            IReadOnlyList<ProtoArchiveInfo> list = M.ListArchives();
            Assert.AreEqual(1, list.Count, "the single-slot backup is a separate file, not an archive");
            Assert.AreEqual("archive-only", M.Load(list[0].FileName).PlayerName, "...and it did not overwrite the archive");
        }

        [Test]
        public void ListArchives_WhenNoneWritten_IsEmpty()
        {
            Assert.AreEqual(0, M.ListArchives().Count);
        }
    }
}
