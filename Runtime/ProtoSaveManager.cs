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
    /// Though, you can use it with any protobuf generated class that is not intended as a save file, but you want to write to the disk.
    /// 
    /// Subclass it and provide all `abstract` implementations.
    /// Tips : If you name your class as `LocalSave`, then you will be able to nicely type `LocalSave.Save()` or `LocalSave.Manager` and do things.
    /// 
    /// **Active save** : an *in-memory* save *data* game is using via <see cref="Active">. That property loads a default save from disk called "main save file".
    /// 
    /// **Main save file** : a save *file* stored on *disk* with name <see cref="MainFileName">.
    /// Your active save became this file via <see cref="Save">, 
    /// but you could get a <typeparamref name="SAVE"> from elsewhere and use <see cref="ApplyToActive(SAVE)"> to set it as active.
    /// Nowadays games are mostly single-save and autosaved, so it is convenient to keep overwriting the same file by default.
    /// </summary>
    /// <typeparam name="SAVE">The type of your save file that should be from Protobuf.</typeparam>
    /// <typeparam name="SELF">Throw the name of your subclass itself into this type param for `static` magic <see cref="Active"> and <see cref="Manager"> to work.</typeparam>
    public abstract class ProtoSaveManager<SAVE, SELF>
    where SAVE : ILocalSave<SAVE>, IMessage<SAVE>, new()
    where SELF : ProtoSaveManager<SAVE, SELF>, new()
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
        /// Specify what to do if the save file is found but encountered <see cref="CryptographicException"> exception while loading it.
        /// Usually this is when you changed the save file's structure and the old key could no longer be used.
        /// </summary>
        protected virtual SAVE Migration(string problematicFilePathWithExtension) => new SAVE().CreateEmptySaveFile();

        /// <summary>
        /// Specify what to do after successfully loading each save from disk.
        /// You may try to prevent players hacking your local save file here.
        /// </summary>
        protected virtual SAVE Validation(SAVE loadedSaveData) => loadedSaveData;

        /// <summary>
        /// For simple stupid encoding in <see cref="SaveDataFromStream(Stream)"> and <see cref="SaveDataToStream(SAVE)">.
        /// If you override those, this has no use anymore.
        /// 
        /// If someone know you are using this utility and found
        /// your string that is the target of this property, your save probably could be easily hacked.
        /// (But at least it is not in plain text if you are satisfied with that.)
        /// </summary>
        protected abstract string length8KeyString { get; }

        private byte[] key => Encoding.ASCII.GetBytes(length8KeyString.Substring(0, 8));

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

        private static SAVE active;

        /// <summary>
        /// The manager gives you 1 special loaded in-memory save data slot by loading from a main save file.
        /// It automatically loads on using this property if not yet.
        /// </summary>
        public static SAVE Active
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
        /// A quick save which save the <see cref="Active"> save data to main file.
        /// </summary>
        public static void Save() => Manager.Save(active, $"{Manager.MainFileName}");

        /// <summary>
        /// Backup the <see cref="Active"> save to a new backup file.
        /// 
        /// There is no access point for this backup files yet. Just as a safety net to backup occassionally
        /// and in the case that the save corrupted (by your mistake or disk failure), at least your player could 
        /// dig the backup and see if it works or not.
        /// 
        /// TODO : Make this method backup incrementally as multiple files, with timestamp.
        /// </summary>
        public void BackupActive() => Save(active, $"{Manager.MainFileName}{Manager.BackupSuffix}");

        /// <summary>
        /// Reload main save file into <see cref="Active"> slot, discarding all unsaved changes.
        /// </summary>
        public void ReloadActive() => Manager.ApplyToActive(Manager.LoadMain());

        /// <summary>
        /// For example getting a save restore as a JSON sent from server.
        /// </summary>
        public SAVE DecodeSaveFromBase64(string base64String) => DecodeSaveFromBytes(Convert.FromBase64String(base64String));

        public SAVE DecodeSaveFromBytes(byte[] saveBytes)
        {
            using (MemoryStream memStream = new MemoryStream(saveBytes))
            {
                return SaveDataFromStream(memStream);
            }
        }

        public byte[] EncodeSaveToBytes(SAVE save)
        {
            using (var memStream = SaveDataToStream(save))
            {
                return memStream.ToArray();
            }
        }

        /// <summary>
        /// If you want to put the entire save in JSON this is useful.
        /// </summary>
        public string EncodeSaveToBase64(SAVE save) => Convert.ToBase64String(EncodeSaveToBytes(save));

        /// <summary>
        /// Save any save file to overwrite the main save file.
        /// </summary>
        public void Save(SAVE save) => SaveAs(save, $"{Manager.MainFileName}");

        /// <summary>
        /// Save any save file with custom name using the same extension your game had been using.
        /// </summary>
        public void Save(SAVE save, string fileNameWithoutExtension) => SaveAs(save, $"{fileNameWithoutExtension}{Manager.SaveFileExtension}");

        private void SaveAs(SAVE save, string fileNameWithExtension)
        {
            //iOS used to complain about Protobuf doing JIT without this, not sure about now.
            Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER", "yes");

            //Debug.Log("Saved : " + Application.persistentDataPath);
            using (FileStream file = File.Create($"{SaveFolderAbsolute}/{fileNameWithExtension}"))
            using (var memStream = SaveDataToStream(save))
            {
                byte[] bytes = memStream.ToArray();
                file.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// You could override to something less stupid.
        /// </summary>
        public virtual SAVE SaveDataFromStream(Stream stream)
        {
            //iOS used to complain about Protobuf doing JIT without this, not sure about now.
            Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER", "yes");

            //This is a stupid scheme where initialization vector was pasted as a header of the save file, so we just yank it back for use...
            //Edit to something more sophisicated if you don't want to be hacked lol
            byte[] ivRead = new byte[8];
            stream.Read(ivRead, 0, 8);

            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Key = key;
            des.IV = ivRead;

            //Debug.Log($"Using {string.Join(",", des.Key.Select(x => x))} {string.Join(",", des.IV.Select(x => x))}");

            SAVE loadedData = new SAVE();
            using (var cryptoStream = new CryptoStream(stream, des.CreateDecryptor(), CryptoStreamMode.Read))
            {
                using (Google.Protobuf.CodedInputStream cis = new Google.Protobuf.CodedInputStream(cryptoStream))
                {
                    loadedData = new MessageParser<SAVE>(() => new SAVE()).ParseFrom(cis);
                    var validated = Validation(loadedData);
                    return validated;
                }
            }
        }

        /// <summary>
        /// You could override to something less stupid.
        /// </summary>
        public virtual MemoryStream SaveDataToStream(SAVE save)
        {
            MemoryStream memStream = new MemoryStream();
            DESCryptoServiceProvider des = new DESCryptoServiceProvider();
            des.Key = key;
            des.GenerateIV();

            //This is a stupid scheme where initialization vector is pasted as a header of the save file. (At least it is generated)
            //Edit to something more sophisicated if you don't want to be hacked lol
            memStream.Write(des.IV, 0, 8);

            //Debug.Log("Writing " + BitConverter.ToString(des.IV));

            using (var cryptoStream = new CryptoStream(memStream, des.CreateEncryptor(), CryptoStreamMode.Write))
            {
                using (Google.Protobuf.CodedOutputStream cos = new Google.Protobuf.CodedOutputStream(cryptoStream))
                {
                    save.WriteTo(cos);
                }
            }
            return memStream;
        }

        /// <summary>
        /// Load and return the main save file. It's state may be behind of the current <see cref="Active"> save file.
        /// You could use this for progress comparison before overwriting, for example.
        /// 
        /// If there is no save file, you get a fresh save instead of an exception.
        /// </summary>
        public SAVE LoadMain()
        {
            try
            {
                return Load($"{MainFileName}");
            }
            catch (FileNotFoundException)
            {
                return new SAVE().CreateEmptySaveFile();
            }
        }

        /// <summary>
        /// Load any save file in the <see cref="InnerSaveFolder">. Use <see cref="ApplyToActive(SAVE)"> to make the returned save data the <see cref="Active"> save.
        /// </summary>
        public SAVE Load(string fileNameWithoutExtension) => LoadFromPath($"{SaveFolderAbsolute}/{fileNameWithoutExtension}{SaveFileExtension}");

        /// <summary>
        /// Overwrite an <see cref="Active"> save slot with an arbitrary save data.
        /// </summary>
        public void ApplyToActive(SAVE save) => active = save;

        /// <summary>
        /// A destructive operation that turn back the <see cref="Active"> save to clean state.
        /// The main save file remains intact until you <see cref="Save"> it for real.
        /// </summary>
        public void ResetActive() => ApplyToActive(new SAVE().CreateEmptySaveFile());

        private SAVE LoadFromPath(string path)
        {
            //Debug.Log($"Loading from {path}");
            if (File.Exists(path))
            {
                try
                {
                    using (FileStream fileStream = File.Open(path, FileMode.Open))
                    {
                        SAVE loaded = SaveDataFromStream(fileStream);
                        return loaded;
                    }
                }
                catch (CryptographicException)//ce1)
                {
                    //Migration only available when finding the file but not readable.
                    // Debug.LogWarning(ce1);
                    // Debug.LogWarning("Possible old save data or corrupt save data found, trying to migrate.");
                    try
                    {
                        var migrated = Migration(path);
                        // Debug.Log("Migration complete");
                        return Validation(migrated);
                    }
                    catch (CryptographicException)//ce2)
                    {
                        // Debug.LogWarning(ce2);
                        // Debug.LogWarning("Could not migrate. Creating a new save file.");
                        return new SAVE().CreateEmptySaveFile(); //you get an empty save if migration also throws crypto
                    }
                }
            }
            else
            {
                throw new FileNotFoundException($"Save file not found at path {path}");
            }
        }

        #if UNITY_EDITOR

                /// <summary>
                /// Useful in testing.
                /// Do not include `Assets` or leading slash in the <paramref name="path">.
                /// </summary>
                public SAVE LoadFromProject(string path, string name) => LoadFromPath($"{Application.dataPath}/{path}/{name}{SaveFileExtension}");

                /// <summary>
                /// Useful in testing.
                /// Do not include `Assets` or leading slash in the <paramref name="path">.
                /// </summary>
                public void ApplyFromProjectToActive(string path, string name) => ApplyToActive(LoadFromProject(path, name));

        #endif

    }
}