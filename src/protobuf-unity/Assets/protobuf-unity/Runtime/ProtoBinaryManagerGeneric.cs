using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace E7.Protobuf
{
    /// <summary>
    /// Selects the PBKDF2 hash and the AES key size used to derive the encryption key.
    /// The default stays on the legacy scheme so existing save files remain readable; picking another
    /// value changes the derived key, making previously written saves unreadable without a migration.
    /// </summary>
    public enum ProtoEncryptionScheme
    {
        Sha1Aes128 = 0,
        Sha256Aes256,
        Sha512Aes256,
    }

    /// <summary>
    /// One entry in the rolling archive history produced by
    /// <see cref="ProtoBinaryManager{PROTO, SELF}.ArchiveActive"/>. Returned from
    /// <see cref="ProtoBinaryManager{PROTO, SELF}.ListArchives"/> so you can present the available
    /// snapshots or assert against them in tests.
    /// </summary>
    public readonly struct ProtoArchiveInfo
    {
        /// <summary>
        /// The calendar day this archive was written, parsed from its file name's <c>yyyy-MM-dd</c> stamp.
        /// </summary>
        public readonly DateTime Date;

        /// <summary>
        /// The file name without its extension, i.e. the argument you would pass to
        /// <see cref="ProtoBinaryManager{PROTO, SELF}.Load(string)"/>.
        /// </summary>
        public readonly string FileName;

        /// <summary>
        /// Absolute path to the archive file on disk.
        /// </summary>
        public readonly string Path;

        public ProtoArchiveInfo(DateTime date, string fileName, string path)
        {
            Date = date;
            FileName = fileName;
            Path = path;
        }

        public override string ToString() =>
            Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " (" + FileName + ")";
    }

    internal static class ProtoBinaryManagerStaticReset
    {
        private static Action resets;

        internal static void Register(Action reset) => resets += reset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll() => resets?.Invoke();
    }

    /// <summary>
    /// This is a manager for dealing with local save file designed from Protobuf.
    /// </summary>
    /// <remarks>
    /// However You can use it with just about any protobuf generated class that you want to write it to the disk.
    /// But in the documentation I will refer to your proto data as "save file".
    /// 
    /// As you know protobuf generates a C# class with `partial`,
    /// this manager connects to that loosely via type params and interface, 
    /// no subclassing from that actual `partial` data class required.
    /// Instead you subclass a specialized manager from this `abstract` class.
    /// 
    /// Tips : If you name your class as `LocalSave`, then you will be able to nicely type
    /// `LocalSave.Save()` or `LocalSave.Manager` and do things.
    /// 
    /// **Active save** : an *in-memory* save *data* game is using via <see cref="Active"/>.
    /// <see cref="Active"/> loads a default save from disk called "main save file", or use a cached one if loaded already.
    /// 
    /// **Main save file** : a save *file* stored on *disk* with name <see cref="MainFileName"/>.
    /// Your active save became this file via <see cref="Save()"/>,
    /// 
    /// You could get a <typeparamref name="PROTO"/> from elsewhere
    /// and use <see cref="ApplyToActive(PROTO)"/> to set it as active.
    /// 
    /// Nowadays games are mostly single-save and autosaved, so it is convenient to keep overwriting the same file by default.
    /// Many methods in this class was designed to deal with the active slot.
    /// </remarks>
    /// <typeparam name="PROTO">The type of your protobuf generated class.</typeparam>
    /// <typeparam name="SELF">Throw the name of your manager subclass itself into this type param
    /// for `static` magic <see cref="Active"/> and <see cref="Manager"/> to happen.</typeparam>
    public abstract class ProtoBinaryManager<PROTO, SELF>
        where PROTO : IMessage<PROTO>, new()
        where SELF : ProtoBinaryManager<PROTO, SELF>, new()
    {
        /// <summary>
        /// Appended **before** the usual file name's extension for <see cref="BackupActive"/>.
        /// </summary>
        protected virtual string BackupSuffix => ".backup";

        /// <summary>
        /// Appended before the date stamp for rolling archive files written by <see cref="ArchiveActive"/>,
        /// e.g. <c>SaveData.archive.2026-03-14.save</c>. Keep this different from <see cref="BackupSuffix"/>
        /// so the versioned archives never collide with the single-slot backup.
        /// </summary>
        protected virtual string ArchiveSuffix => ".archive";

        /// <summary>
        /// Minimum spacing, in days, between two archives that are kept side by side.
        /// Call <see cref="ArchiveActive"/> as often as you like; while the newest archive is younger than
        /// this it is simply refreshed in place, so 30 keeps roughly one snapshot per month, 7 per week.
        /// </summary>
        protected virtual int ArchiveIntervalDays => 30;

        /// <summary>
        /// How many archives to keep. Once <see cref="ArchiveActive"/> has written a fresh one it prunes the
        /// oldest beyond this count. A value of <c>0</c> or less means "never prune" (keep every interval forever).
        /// This never touches the single-slot backup written by <see cref="BackupActive"/>.
        /// </summary>
        protected virtual int ArchiveRetentionCount => 6;

        /// <summary>
        /// The clock used to date-stamp archives and to decide whether an interval has elapsed.
        /// Defaults to <see cref="DateTime.Now"/>; override it in tests to simulate months passing without waiting.
        /// </summary>
        protected virtual DateTime Now => DateTime.Now;

        /// <summary>
        /// Date format for the archive file name's stamp. ISO <c>yyyy-MM-dd</c> so that a plain lexical sort of
        /// file names is also a chronological sort, which is what makes listing and pruning cheap and reliable.
        /// </summary>
        private const string ArchiveDateFormat = "yyyy-MM-dd";

        /// <summary>
        /// A save file associated with <see cref="Active"/> save data slot. This is without extension.
        /// </summary>
        protected virtual string MainFileName => "SaveData";

        /// <summary>
        /// Default extension for files generated from this manager.
        /// </summary>
        /// <remarks>
        /// This maybe important when your user has a problem, then you could tell him to look for
        /// a file with certain extension and copy that out. So having an easily identifiable helps.
        /// </remarks>
        protected virtual string SaveFileExtension => ".save";

        private string SaveFolderAbsolute => $"{Application.persistentDataPath}/{InnerSaveFolder}";
        private string MainSaveFilePath => $"{SaveFolderAbsolute}/{MainFileName}{SaveFileExtension}";

        /// <summary>
        /// A folder continued from <see cref="Application.persistentDataPath"/> which the save file will be in.
        /// </summary>
        /// <remarks>
        /// Do not add ending slash. Use empty string to place the save file at persistent path root.
        /// </remarks>
        protected virtual string InnerSaveFolder => string.Empty;

        /// <summary>
        /// Specify what to do if the save file is found but throw <see cref="CryptographicException"/>
        /// or <see cref="ArgumentException"/> exception while loading it.
        /// </summary>
        /// <remarks>
        /// Usually this is when you changed the save file's structure and the old key could no longer be used,
        /// or hacker did something with the save file.
        /// </remarks>
        protected virtual PROTO Migration(string problematicFilePathWithExtension) => new PROTO();

        /// <summary>
        /// Specify what to do after successfully loading each save from disk.
        /// </summary>
        /// <remarks>
        /// For example, you may try to prevent players hacking your local save file by further checking against saved hash here.
        /// </remarks>
        protected virtual PROTO Validation(PROTO loadedSaveData) => loadedSaveData;

        /// <summary>
        /// A password string to derive into key,
        /// for AES encoding in <see cref="FromStream(Stream)"/> and <see cref="ToStream(PROTO)"/>, 
        /// </summary>
        /// <remarks>
        /// This is a getter property, if your game is completely client-side you can choose your own poison, for example : 
        /// - Hard code it (e.g. `=> "My password is easily disassembled"`)
        /// - Load it from an another static code, which is obfuscated.
        /// - Make unmanaged call to non-C# code or DLL and get your stuff.
        /// 
        /// Protobuf encoding is not an encryption, the bit patterns are documented [here](https://developers.google.com/protocol-buffers/docs/encoding)
        /// 
        /// Also you could override both <see cref="FromStream(Stream)"/> and <see cref="ToStream(PROTO)"/>
        /// and provide your own algorithm.
        /// </remarks>
        protected abstract string EncryptionPassword { get; }

        /// <summary>
        /// A salt string to derive into key,
        /// for AES encoding in <see cref="FromStream(Stream)"/> and <see cref="ToStream(PROTO)"/>, 
        /// </summary>
        /// <remarks>
        /// Note that usually salt should be regenerated and provided together with the cipher text.
        /// The default implementation uses fixed salt without providing the salt along with the encrypted data,
        /// so it kind of defeat the purpose other than making the KDF work.
        /// 
        /// If you want to do it properly, when you override those then you can use this property as generated salt 
        /// then paste the salt along with the data.
        /// </remarks>
        protected abstract string EncryptionSalt { get; }

        /// <summary>
        /// Iteration count for [PBKDF2](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes),
        /// which turns your password and salt into an encryption key.
        /// </summary>
        /// <remarks>
        /// The point of derivation is so that the key looks inhumanly random while coming from something human
        /// like a password string.
        /// </remarks>
        protected abstract int EncryptionIteration { get; }

        /// <summary>
        /// Override to upgrade the key-derivation hash and AES key size.
        /// </summary>
        protected virtual ProtoEncryptionScheme EncryptionScheme => ProtoEncryptionScheme.Sha1Aes128;

        private Rfc2898DeriveBytes derivator;
        private byte[] key;

        public ProtoBinaryManager()
        {
            // If you use new line in your password or salt string, 
            // Windows machine could produce different result because it uses \r\n instead of just \n
            var password = EncryptionPassword.Replace("\r", "");
            var salt = EncryptionSalt.Replace("\r", "");
            (HashAlgorithmName hash, int keyBytes) = EncryptionScheme switch
            {
                ProtoEncryptionScheme.Sha256Aes256 => (HashAlgorithmName.SHA256, 32),
                ProtoEncryptionScheme.Sha512Aes256 => (HashAlgorithmName.SHA512, 32),
                _ => (HashAlgorithmName.SHA1, 16),
            };
            derivator = new Rfc2898DeriveBytes(Encoding.ASCII.GetBytes(password), Encoding.ASCII.GetBytes(salt),
                EncryptionIteration, hash);
            key = derivator.GetBytes(keyBytes);
        }

        private static SELF manager;

        /// <summary>
        /// This is an entry point to advanced methods.
        /// </summary>
        public static SELF Manager
        {
            get
            {
                if (manager == null)
                {
                    manager = new SELF();
                }

                return manager;
            }
        }

        private static PROTO active;

        public static void ClearStaticState()
        {
            active = default;
            manager = null;
        }

        static ProtoBinaryManager()
        {
            ProtoBinaryManagerStaticReset.Register(ClearStaticState);
        }

        /// <summary>
        /// The manager gives you 1 special loaded in-memory save data slot by loading from the "main save file".
        /// It automatically loads on using this property if not yet.
        /// </summary>
        /// <remarks>
        /// If you don't want a reference to this assembly everywhere you use this property,
        /// you could make a new `Active` in your subclass as `new`, like this :
        /// <code>
        /// public static new PlayerData Active => ProtoSaveManager&lt;YourData, YourSubclassName&gt;.Active;
        /// </code>
        /// </remarks>
        public static PROTO Active
        {
            get
            {
                if (active == null)
                {
                    Manager.ReloadActive();
                }

                return active;
            }
        }

        /// <summary>
        /// The easiest save method, which save the <see cref="Active"/> save data to main save file.
        /// </summary>
        public static void Save() => Manager.Save(active, $"{Manager.MainFileName}");

        /// <summary>
        /// Backup the <see cref="Active"/> save to a new backup file.
        /// </summary>
        /// <remarks>
        /// You can use this as a safety net to backup occassionally
        /// and in the case that the save corrupted (by your mistake or disk failure), at least your player could 
        /// dig the backup and see if it works or not.
        /// 
        /// Or you could use the built-in <see cref="RestoreFromBackup"/> to replace the active save memory with the backup.
        ///
        /// This is the single-slot backup and stays a single slot on purpose. For a rolling history of
        /// dated archives that keeps older versions around, use <see cref="ArchiveActive"/> instead.
        /// </remarks>
        public void BackupActive() => Save(active, $"{Manager.MainFileName}{Manager.BackupSuffix}");

        /// <summary>
        /// Reload from the main (single-slot) backup file written by <see cref="BackupActive"/>.
        /// For the rolling archives see <see cref="RestoreFromArchive"/>.
        /// </summary>
        public void RestoreFromBackup() =>
            Manager.ApplyToActive(Manager.Load($"{Manager.MainFileName}{Manager.BackupSuffix}"));

        // ------------------------------------------------------------------------------------------
        // Rolling archives (versioned backups)
        //
        // Unlike the single-slot backup above, these leave older snapshots behind so you can recover
        // a save from weeks or months ago. Each archive is one dated file; call ArchiveActive() as
        // often as you want and it self-throttles to one snapshot per ArchiveIntervalDays, pruning
        // anything past ArchiveRetentionCount. The single-slot backup is never affected.
        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Write the <see cref="Active"/> save into the rolling archive history, then prune old archives
        /// down to <see cref="ArchiveRetentionCount"/>.
        /// </summary>
        /// <remarks>
        /// Safe to call as often as you like (every launch, every autosave). While the newest archive is
        /// younger than <see cref="ArchiveIntervalDays"/> this refreshes it in place — so repeated calls
        /// inside one interval collapse to a single, up-to-date snapshot instead of piling up. Once an
        /// interval has elapsed the previous archive is frozen and a new dated one is opened, giving you
        /// "last month's save", "the month before", and so on.
        ///
        /// The single-slot backup from <see cref="BackupActive"/> is a completely separate file and is
        /// never read, written, or pruned by any of the archive methods.
        /// </remarks>
        /// <returns>Information about the archive that now holds the active save.</returns>
        public ProtoArchiveInfo ArchiveActive()
        {
            IReadOnlyList<ProtoArchiveInfo> existing = ListArchives();
            DateTime today = Now.Date;

            if (existing.Count > 0)
            {
                ProtoArchiveInfo newest = existing[0];
                if ((today - newest.Date).TotalDays < ArchiveIntervalDays)
                {
                    // Still inside the current interval window: replace the current snapshot in place
                    // (re-dated to today) rather than leaving a near-duplicate behind.
                    File.Delete(newest.Path);
                }
                // else: the newest archive is at least one full interval old, so leave it frozen forever
                //       and fall through to open a brand new dated archive next to it.
            }

            string fileNameWithoutExtension = ArchiveFileName(today);
            Save(Active, fileNameWithoutExtension);

            if (ArchiveRetentionCount > 0)
            {
                PruneArchives(ArchiveRetentionCount);
            }

            string path = $"{SaveFolderAbsolute}/{fileNameWithoutExtension}{SaveFileExtension}";
            return new ProtoArchiveInfo(today, fileNameWithoutExtension, path);
        }

        /// <summary>
        /// Every archive currently on disk, newest first. Index 0 is the most recent snapshot,
        /// index 1 is one interval back, and so on — the same ordering <see cref="RestoreFromArchive"/> uses.
        /// </summary>
        /// <remarks>
        /// Handy for building a "restore from a previous save" UI, and for asserting behaviour in tests.
        /// The single-slot backup is intentionally excluded.
        /// </remarks>
        public IReadOnlyList<ProtoArchiveInfo> ListArchives()
        {
            var result = new List<ProtoArchiveInfo>();
            string folder = SaveFolderAbsolute;
            if (!Directory.Exists(folder))
            {
                return result;
            }

            string prefix = $"{MainFileName}{ArchiveSuffix}.";
            string extension = SaveFileExtension;
            foreach (string path in Directory.GetFiles(folder))
            {
                string fileName = Path.GetFileName(path);
                if (!fileName.StartsWith(prefix, StringComparison.Ordinal) ||
                    !fileName.EndsWith(extension, StringComparison.Ordinal))
                {
                    continue;
                }

                string stamp = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - extension.Length);
                if (!DateTime.TryParseExact(stamp, ArchiveDateFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime date))
                {
                    continue; // ignore anything that merely looks like an archive but has no valid date stamp
                }

                string nameWithoutExtension = fileName.Substring(0, fileName.Length - extension.Length);
                result.Add(new ProtoArchiveInfo(date, nameWithoutExtension, path));
            }

            // Newest first. Break date ties by file name so ordering is deterministic.
            result.Sort((a, b) =>
            {
                int byDate = b.Date.CompareTo(a.Date);
                return byDate != 0 ? byDate : string.CompareOrdinal(b.FileName, a.FileName);
            });
            return result;
        }

        /// <summary>
        /// Replace the <see cref="Active"/> save with an archived snapshot from <paramref name="intervalsBack"/>
        /// intervals ago. <c>0</c> is the most recent archive, <c>1</c> the one before it, and so on.
        /// </summary>
        /// <remarks>
        /// This only changes the in-memory active slot, exactly like <see cref="RestoreFromBackup"/>.
        /// Call <see cref="Save()"/> afterwards if you want the restored data to become the main save file.
        /// </remarks>
        /// <exception cref="FileNotFoundException">There is no archive that far back.</exception>
        public void RestoreFromArchive(int intervalsBack = 0)
        {
            IReadOnlyList<ProtoArchiveInfo> archives = ListArchives();
            if (intervalsBack < 0 || intervalsBack >= archives.Count)
            {
                throw new FileNotFoundException(
                    $"No archive {intervalsBack} interval(s) back; there are only {archives.Count} archive(s).");
            }

            ApplyToActive(Load(archives[intervalsBack].FileName));
        }

        /// <summary>
        /// Delete every archive except the newest <paramref name="keepCount"/>. This is the "prune to the last
        /// N intervals" operation; it never touches the single-slot backup.
        /// </summary>
        /// <param name="keepCount">How many of the most recent archives to keep. <c>0</c> or less removes them all.</param>
        /// <returns>The number of archive files deleted.</returns>
        public int PruneArchives(int keepCount)
        {
            IReadOnlyList<ProtoArchiveInfo> archives = ListArchives();
            int keep = Math.Max(0, keepCount);
            int deleted = 0;
            for (int i = keep; i < archives.Count; i++)
            {
                File.Delete(archives[i].Path);
                deleted++;
            }

            return deleted;
        }

        /// <summary>
        /// Delete all archives. The single-slot backup from <see cref="BackupActive"/> is left untouched.
        /// </summary>
        /// <returns>The number of archive files deleted.</returns>
        public int ClearArchives() => PruneArchives(0);

        private string ArchiveFileName(DateTime date) =>
            $"{MainFileName}{ArchiveSuffix}.{date.ToString(ArchiveDateFormat, CultureInfo.InvariantCulture)}";

        /// <summary>
        /// Reload main save file into <see cref="Active"/> slot, discarding all unsaved changes.
        /// </summary>
        public void ReloadActive() => Manager.ApplyToActive(Manager.LoadMain());

        /// <summary>
        /// For example getting a save restore as a JSON sent from server.
        /// </summary>
        public PROTO FromBase64(string base64String) => FromBytes(Convert.FromBase64String(base64String));

        public PROTO FromBytes(byte[] saveBytes)
        {
            using (MemoryStream memStream = new MemoryStream(saveBytes))
            {
                return FromStream(memStream);
            }
        }

        /// <summary>
        /// By default this returns AES encrypted protobuf bytes.
        /// </summary>
        public byte[] ToBytes(PROTO save)
        {
            using (var memStream = ToStream(save))
            {
                return memStream.ToArray();
            }
        }

        /// <summary>
        /// If you want to put the entire save in JSON this is useful.
        /// </summary>
        public string ToBase64(PROTO save) => Convert.ToBase64String(ToBytes(save));

        /// <summary>
        /// Save any save file to overwrite the main save file.
        /// </summary>
        public void Save(PROTO save) => Save(save, $"{Manager.MainFileName}");

        /// <summary>
        /// Save any save file with custom name using the same extension your game had been using.
        /// </summary>
        public void Save(PROTO save, string fileNameWithoutExtension) =>
            SaveAs(save, $"{fileNameWithoutExtension}{Manager.SaveFileExtension}");

        private void SaveAs(PROTO save, string fileNameWithExtension)
            => ProtoBinaryManager.StreamToFile(ToStream(save), SaveFolderAbsolute, fileNameWithExtension);

        /// <summary>
        /// The default implementation picks up 16 bytes AES IV from the front of cipher text, 
        /// use it together with a key derived from  <see cref="EncryptionPassword"/>
        /// and <see cref="EncryptionSalt"/> to AES decode the protobuf stream.
        /// 
        /// You could override to something more sophisticated if you want.
        /// </summary>
        public virtual PROTO FromStream(Stream stream)
        {
            var loadedData = ProtoBinaryManager.ProtoFromStream<PROTO>(stream, key);
            var validated = Validation(loadedData);
            return validated;
        }

        /// <summary>
        /// The default implementation applies basic AES encryption with a key
        /// derived from <see cref="EncryptionPassword"/> and <see cref="EncryptionSalt"/>,
        /// with generated IV pasted in front of cipher text, before writing to the disk.
        /// 
        /// You could override to something more sophisticated if you want.
        /// </summary>
        public virtual MemoryStream ToStream(PROTO save) => ProtoBinaryManager.ProtoToStream(save, key);

        /// <summary>
        /// Load and return the main save file. Its state may be behind of the current <see cref="Active"/> save file.
        /// </summary>
        /// <remarks>
        /// You could use this for progress comparison in the save overwriting dialog, for example.
        /// 
        /// If there is no save file, you get a fresh save instead of an exception. This fresh save is just `new`.
        /// 
        /// Protobuf generated `OnConstruction()` `partial` method for you to add your own custom logic,
        /// which the constructor will call into.
        /// </remarks>
        public PROTO LoadMain()
        {
            try
            {
                return Load($"{MainFileName}");
            }
            catch (FileNotFoundException)
            {
                return new PROTO();
            }
        }

        /// <summary>
        /// Load any save file in the <see cref="InnerSaveFolder"/>.
        /// Use <see cref="ApplyToActive(PROTO)"/> to make the returned save data the <see cref="Active"/> save.
        /// </summary>
        public PROTO Load(string fileNameWithoutExtension) =>
            FromFile($"{SaveFolderAbsolute}", $"{fileNameWithoutExtension}{SaveFileExtension}");

        /// <summary>
        /// Overwrite an <see cref="Active"/> save slot with an arbitrary save data.
        /// </summary>
        public void ApplyToActive(PROTO save) => active = save;

        /// <summary>
        /// A destructive operation that turn back the <see cref="Active"/> save to clean state.
        /// But it is only in active slot which is in your memory.
        /// The main physical save file remains intact until you <see cref="Save()"/> it for real.
        /// </summary>
        public void ResetActive() => ApplyToActive(new PROTO());

        /// <summary>
        /// Unlike <see cref="ProtoBinaryManager.ProtoFromFile{PROTO}(byte[], string, string)"/>,
        /// it has some recovery options when the file is hacked or corrupted.
        /// (but the file must exist, otherwise it will throw <see cref="FileNotFoundException"/> as usual)
        /// </summary>
        private PROTO FromFile(string loadFolderAbsolute, string fileNameWithExtension)
        {
            string path = $"{loadFolderAbsolute}/{fileNameWithExtension}";
            try
            {
                return ProtoBinaryManager.ProtoFromFile<PROTO>(key, loadFolderAbsolute, fileNameWithExtension);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is ArgumentException ||
                                       ex is InvalidOperationException)
            {
                /*
                    Invalid operation occurs when protobuf goes : 
                    InvalidOperationException: Wire Type is invalid.
                    at Google.Protobuf.UnknownFieldSet.MergeFieldFrom
                 */

                //Migration only available when found the file but not readable.
#if UNITY_EDITOR
                Debug.LogWarning(ex);
                Debug.LogWarning("Possible old save data or corrupt save data found, trying to migrate.");
#endif
                try
                {
                    var migrated = Migration(path);
#if UNITY_EDITOR
                    Debug.Log("Migration complete");
#endif
                    return Validation(migrated);
                }
                catch (Exception ex2) when (ex2 is CryptographicException || ex2 is ArgumentException ||
                                            ex2 is InvalidOperationException)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(ex2);
                    Debug.LogWarning("Could not migrate. Creating a new save file.");
#endif
                    return new PROTO(); //you get an empty save if migration also throws crypto
                }
            }
        }

#if UNITY_EDITOR

        /// <summary>
        /// Useful in unit testing. You could have a sample of old version saves from player and test your compatibility with them.
        /// Or just a way to setup the test for specific scenario you want to check out based on preset save files.
        /// </summary>
        /// <remarks>
        /// Do not include `Assets` or leading slash in the <paramref name="path"/>.
        /// File name don't need extension, it uses <see cref="SaveFileExtension"/> in your subclassed manager class.
        /// </remarks>
        public PROTO FromProject(string path, string name) =>
            FromFile($"{Application.dataPath}/{path}", $"{name}{SaveFileExtension}");

        /// <summary>
        /// Useful in unit testing. You could have a sample of old version saves from player and test your compatibility with them.
        /// Or just a way to setup the test for specific scenario you want to check out based on preset save files.
        /// </summary>
        /// <remarks>
        /// Do not include `Assets` or leading slash in the <paramref name="path"/>.
        /// File name don't need extension, it uses <see cref="SaveFileExtension"/> in your subclassed manager class.
        /// </remarks>
        public void ApplyFromProjectToActive(string path, string name) => ApplyToActive(FromProject(path, name));

#endif
    }
}