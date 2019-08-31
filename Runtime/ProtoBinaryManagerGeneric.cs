using Google.Protobuf;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace E7.Protobuf
{
    /// <summary>
    /// This is a manager for dealing with local save file designed from Protobuf.
    /// However You can use it with just about any protobuf generated class that you want to write it to the disk.
    /// But in the documentation I will refer to your proto data as "save file".
    /// 
    /// As you know protobuf generates a C# class with `partial`, this manager connects to that loosely via type params and interface, 
    /// no subclassing from that actual `partial` data class required. Instead you subclass a specialized manager from this `abstract` class.
    /// 
    /// Tips : If you name your class as `LocalSave`, then you will be able to nicely type `LocalSave.Save()` or `LocalSave.Manager` and do things.
    /// 
    /// **Active save** : an *in-memory* save *data* game is using via <see cref="Active">.
    /// <see cref="Active"> loads a default save from disk called "main save file", or use a cached one if loaded already.
    /// 
    /// **Main save file** : a save *file* stored on *disk* with name <see cref="MainFileName">.
    /// Your active save became this file via <see cref="Save">, 
    /// but you could get a <typeparamref name="PROTO"> from elsewhere and use <see cref="ApplyToActive(PROTO)"> to set it as active.
    /// 
    /// Nowadays games are mostly single-save and autosaved, so it is convenient to keep overwriting the same file by default.
    /// Many methods in this class was designed to deal with the active slot.
    /// </summary>
    /// <typeparam name="PROTO">The type of your protobuf generated class.</typeparam>
    /// <typeparam name="SELF">Throw the name of your manager subclass itself into this type param for `static` magic <see cref="Active"> and <see cref="Manager"> to happen.</typeparam>
    public abstract class ProtoBinaryManager<PROTO, SELF>
    where PROTO : IMessage<PROTO>, new()
    where SELF : ProtoBinaryManager<PROTO, SELF>, new()
    {
        /// <summary>
        /// Appended **before** the usual file name's extension for <see cref="BackupActive">.
        /// </summary>
        protected virtual string BackupSuffix => ".backup";

        /// <summary>
        /// A save file associated with <see cref="Active"> save data slot. This is without extension.
        /// </summary>
        protected virtual string MainFileName => "SaveData";

        /// <summary>
        /// Default extension for files generated from this manager.
        /// 
        /// This maybe important when your user has a problem, then you could tell him to look for
        /// a file with certain extension and copy that out. So having an easily identifiable helps.
        /// </summary>
        protected virtual string SaveFileExtension => ".save";

        private string SaveFolderAbsolute => $"{Application.persistentDataPath}/{InnerSaveFolder}";
        private string MainSaveFilePath => $"{SaveFolderAbsolute}/{MainFileName}{SaveFileExtension}";

        /// <summary>
        /// A folder continued from <see cref="Application.persistentDataPath"> which the save file will be in.
        /// Do not add ending slash. Use empty string to place the save file at persistent path root.
        /// </summary>
        protected virtual string InnerSaveFolder => string.Empty;

        /// <summary>
        /// Specify what to do if the save file is found but throw <see cref="CryptographicException"> or <see cref="ArgumentException"> exception while loading it.
        /// Usually this is when you changed the save file's structure and the old key could no longer be used.
        /// </summary>
        protected virtual PROTO Migration(string problematicFilePathWithExtension) => new PROTO();

        /// <summary>
        /// Specify what to do after successfully loading each save from disk.
        /// For example, you may try to prevent players hacking your local save file by further checking against saved hash here.
        /// </summary>
        protected virtual PROTO Validation(PROTO loadedSaveData) => loadedSaveData;

        /// <summary>
        /// A password for simple encoding in <see cref="FromStream(Stream)"> and <see cref="ToStream(PROTO)">, 
        /// 
        /// This is a getter property, if your game is completely client-side you can choose your own poison, for example : 
        /// - Hard code it (e.g. `=> "My password is easily disassembled"`)
        /// - Load it from an another static code, which is obfuscated.
        /// - Make unmanaged call to non-C# code or DLL and get your stuff.
        /// 
        /// Protobuf encoding is not an encryption, the bit patterns are documented [here](https://developers.google.com/protocol-buffers/docs/encoding)
        /// 
        /// Also you could override both <see cref="FromStream(Stream)"> and <see cref="ToStream(PROTO)"> and provide your own algorithm.
        /// </summary>
        protected abstract string EncryptionPassword { get; }

        /// <summary>
        /// A password salt for simple encoding in <see cref="FromStream(Stream)"> and <see cref="ToStream(PROTO)">, 
        /// 
        /// This is a getter property, if your game is completely client-side you can choose your own poison, for example : 
        /// - Hard code it (e.g. `=> "My password is easily disassembled"`)
        /// - Load it from an another static code, which is obfuscated.
        /// - Make unmanaged call to non-C# code or DLL and get your stuff.
        /// 
        /// Protobuf encoding is not an encryption, the bit patterns are documented [here](https://developers.google.com/protocol-buffers/docs/encoding)
        /// 
        /// Also you could override both <see cref="FromStream(Stream)"> and <see cref="ToStream(PROTO)"> and provide your own algorithm.
        /// 
        /// Note that usually salt should be regenerated and provided together with the cipher text. The default implementation uses fixed salt
        /// without providing the salt along with the encrypted data, so it kind of defeat the purpose other than making the KDF work.
        /// 
        /// If you want to do it properly, when you override those then you can use this property as generated salt 
        /// then paste the salt along with the data.
        /// </summary>
        protected abstract string EncryptionSalt { get; }

        private Rfc2898DeriveBytes derivator;
        private byte[] key;
        public ProtoBinaryManager()
        {
            derivator = new Rfc2898DeriveBytes(Encoding.ASCII.GetBytes(EncryptionPassword), Encoding.ASCII.GetBytes(EncryptionSalt), 5555);
            key = derivator.GetBytes(16);
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

        /// <summary>
        /// The manager gives you 1 special loaded in-memory save data slot by loading from the "main save file".
        /// It automatically loads on using this property if not yet.
        /// 
        /// If you don't want a reference to this assembly everywhere you use this property,
        /// you could make a new `Active` in your subclass as `new`, like this :
        /// 
        /// <code>
        /// public static new PlayerData Active => ProtoSaveManager<YourData, YourSubclassName>.Active;
        /// </code>
        /// </summary>
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
        /// The easiest save method, which save the <see cref="Active"> save data to main file.
        /// </summary>
        public static void Save() => Manager.Save(active, $"{Manager.MainFileName}");

        /// <summary>
        /// Backup the <see cref="Active"> save to a new backup file.
        /// 
        /// You can use this as a safety net to backup occassionally
        /// and in the case that the save corrupted (by your mistake or disk failure), at least your player could 
        /// dig the backup and see if it works or not.
        /// 
        /// Or you could use the built-in <see cref="RestoreFromBackup"> to replace the active save memory with the backup.
        /// 
        /// TODO : Make this method backup incrementally as multiple files, with timestamp.
        /// </summary>
        public void BackupActive() => Save(active, $"{Manager.MainFileName}{Manager.BackupSuffix}");

        /// <summary>
        /// Reload from the main backup file.
        /// 
        /// TODO : When backup could do multiple files, this should use the most recent one.
        /// </summary>
        public void RestoreFromBackup() => Manager.ApplyToActive(Manager.Load($"{Manager.MainFileName}{Manager.BackupSuffix}"));

        /// <summary>
        /// Reload main save file into <see cref="Active"> slot, discarding all unsaved changes.
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
        public void Save(PROTO save, string fileNameWithoutExtension) => SaveAs(save, $"{fileNameWithoutExtension}{Manager.SaveFileExtension}");

        private void SaveAs(PROTO save, string fileNameWithExtension) 
            => ProtoBinaryManager.StreamToFile(ToStream(save), SaveFolderAbsolute, fileNameWithExtension);

        /// <summary>
        /// The default implementation picks up 16 bytes IV from the front of cipher text, 
        /// use it together with a key derived from  <see cref="EncryptionPassword"> and <see cref="EncryptionSalt"> to AES decode the protobuf stream.
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
        /// The default implementation applies basic AES encryption with a key derived from <see cref="EncryptionPassword"> and <see cref="EncryptionSalt">,
        /// with generated IV pasted in front of cipher text, before writing to the disk.
        /// 
        /// You could override to something more sophisticated if you want.
        /// </summary>
        public virtual MemoryStream ToStream(PROTO save) => ProtoBinaryManager.ProtoToStream(save, key);

        /// <summary>
        /// Load and return the main save file. Its state may be behind of the current <see cref="Active"> save file.
        /// You could use this for progress comparison in the save overwriting dialog, for example.
        /// 
        /// If there is no save file, you get a fresh save instead of an exception. This fresh save is just `new`.
        /// Protobuf generated `OnConstruction()` `partial` method for you to add your own custom logic, which the constructor will call into.
        /// </summary>
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
        /// Load any save file in the <see cref="InnerSaveFolder">. Use <see cref="ApplyToActive(PROTO)"> to make the returned save data the <see cref="Active"> save.
        /// </summary>
        public PROTO Load(string fileNameWithoutExtension) => FromFile($"{SaveFolderAbsolute}", $"{fileNameWithoutExtension}{SaveFileExtension}");

        /// <summary>
        /// Overwrite an <see cref="Active"> save slot with an arbitrary save data.
        /// </summary>
        public void ApplyToActive(PROTO save) => active = save;

        /// <summary>
        /// A destructive operation that turn back the <see cref="Active"> save to clean state.
        /// But it is only in active slot which is in your memory.
        /// The main physical save file remains intact until you <see cref="Save"> it for real.
        /// </summary>
        public void ResetActive() => ApplyToActive(new PROTO());

        /// <summary>
        /// Unlike <see cref="ProtoBinaryManager.ProtoFromFile{PROTO}(byte[], string, string)">, it has some recovery options when the
        /// file is hacked or corrupted. (but the file must exist, otherwise it will throw `FileNotFoundException` as usual)
        /// </summary>
        private PROTO FromFile(string loadFolderAbsolute, string fileNameWithExtension)
        {
            string path = $"{loadFolderAbsolute}/{fileNameWithExtension}";
            try
            {
                return ProtoBinaryManager.ProtoFromFile<PROTO>(key, loadFolderAbsolute, fileNameWithExtension);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is ArgumentException || ex is InvalidOperationException)
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
                catch (Exception ex2) when (ex2 is CryptographicException || ex2 is ArgumentException || ex is InvalidOperationException)
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
        /// 
        /// Do not include `Assets` or leading slash in the <paramref name="path">.
        /// File name don't need extension, it uses <see cref="SaveFileExtension"> in your subclassed manager class.
        /// </summary>
        public PROTO FromProject(string path, string name) => FromFile($"{Application.dataPath}/{path}", $"{name}{SaveFileExtension}");

        /// <summary>
        /// Useful in unit testing. You could have a sample of old version saves from player and test your compatibility with them.
        /// Or just a way to setup the test for specific scenario you want to check out based on preset save files.
        /// 
        /// Do not include `Assets` or leading slash in the <paramref name="path">.
        /// File name don't need extension, it uses <see cref="SaveFileExtension"> in your subclassed manager class.
        /// </summary>
        public void ApplyFromProjectToActive(string path, string name) => ApplyToActive(FromProject(path, name));

#endif

    }
}