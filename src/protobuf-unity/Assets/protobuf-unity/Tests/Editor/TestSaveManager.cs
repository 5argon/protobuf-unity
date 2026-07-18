using System;
using System.IO;
using UnityEngine;

namespace E7.Protobuf.Tests
{
    /// <summary>
    /// Concrete <see cref="ProtoBinaryManager{PROTO, SELF}"/> for the generated <see cref="TestSave"/> message,
    /// used only by the edit-mode tests.
    /// </summary>
    /// <remarks>
    /// On top of the required encryption members it adds three test affordances:
    /// <list type="bullet">
    /// <item>a controllable <see cref="Now"/> clock, so the day/interval logic can be driven across simulated
    /// months without waiting;</item>
    /// <item>overridable <see cref="ArchiveIntervalDays"/> / <see cref="ArchiveRetentionCount"/> knobs; and</item>
    /// <item>an isolated <see cref="InnerSaveFolder"/> under <see cref="Application.persistentDataPath"/> that the
    /// fixtures wipe between tests.</item>
    /// </list>
    /// The knobs are <c>static</c> on purpose: <see cref="ProtoBinaryManager{PROTO, SELF}.ClearStaticState"/>
    /// throws the cached manager instance away between tests, so instance state would not survive.
    /// </remarks>
    public class TestSaveManager : ProtoBinaryManager<TestSave, TestSaveManager>
    {
        /// <summary>Sub-folder of <see cref="Application.persistentDataPath"/> that holds every test file.</summary>
        public const string TestFolderName = "ProtoUnityEditModeTests";

        // --- Encryption (dummy values; the tests only care that data round-trips) ---
        protected override string EncryptionPassword => "edit-mode-test-password";
        protected override string EncryptionSalt => "edit-mode-test-salt";
        protected override int EncryptionIteration => 1000;

        // --- Isolation ---
        protected override string MainFileName => "TestSave";
        protected override string InnerSaveFolder => TestFolderName;

        // --- Controllable clock ---
        public static DateTime? ClockOverride;
        protected override DateTime Now => ClockOverride ?? base.Now;

        // --- Archive knobs the tests reconfigure ---
        public static int IntervalDays = 30;
        public static int RetentionCount = 6;
        protected override int ArchiveIntervalDays => IntervalDays;
        protected override int ArchiveRetentionCount => RetentionCount;

        /// <summary>Set the simulated "today".</summary>
        public static void SetClock(int year, int month, int day) =>
            ClockOverride = new DateTime(year, month, day);

        /// <summary>Reset the static test knobs to their defaults.</summary>
        public static void ResetTestState()
        {
            ClockOverride = null;
            IntervalDays = 30;
            RetentionCount = 6;
        }

        /// <summary>Delete every file this manager may have written under <see cref="TestFolderName"/>.</summary>
        public static void DeleteAllTestFiles()
        {
            string folder = Path.Combine(Application.persistentDataPath, TestFolderName);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// One-call reset for <c>[SetUp]</c>/<c>[TearDown]</c>: clears the cached static save/manager,
        /// resets the test knobs, and wipes the on-disk test folder.
        /// </summary>
        public static void FullReset()
        {
            ClearStaticState();
            ResetTestState();
            DeleteAllTestFiles();
        }
    }
}
