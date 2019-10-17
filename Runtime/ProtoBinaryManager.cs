using Google.Protobuf;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace E7.Protobuf
{
    /// <summary>
    /// Pure utility version of <see cref="ProtoBinaryManager{PROTO, SELF}">.
    /// </summary>
    public static class ProtoBinaryManager
    {
        /// <summary>
        /// Pick up 16 bytes IV from the front of cipher text, use it together with <paramref name="key"> to AES parse the protobuf stream.
        /// </summary>
        /// <typeparam name="P">Your protobuf type here.</typeparam>
        public static P ProtoFromStream<P>(Stream stream, byte[] key) 
        where P : IMessage<P>, new()
        {
            //iOS used to complain about Protobuf doing JIT without this, not sure about now.
            Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER", "yes");

            //This scheme we paste initialization vector as a header of the save file, so we just yank it back for use...
            byte[] ivRead = new byte[16];
            stream.Read(ivRead, 0, 16);

            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            aes.Key = key;
            aes.IV = ivRead;

            //Debug.Log($"Using {string.Join(",", aes.Key.Select(x => x))} {string.Join(",", aes.IV.Select(x => x))}");

            P loadedData = new P();
            using (var cryptoStream = new CryptoStream(stream, aes.CreateDecryptor(), CryptoStreamMode.Read))
            {
                using (Google.Protobuf.CodedInputStream cis = new Google.Protobuf.CodedInputStream(cryptoStream))
                {
                    loadedData = new MessageParser<P>(() => new P()).ParseFrom(cis);
                    // using (MemoryStream ms = new MemoryStream())
                    // {
                    //     using (Google.Protobuf.CodedOutputStream cos = new CodedOutputStream(ms))
                    //     {
                    //         loadedData.WriteTo(cos);
                    //     }
                    //     Debug.Log(loadedData);
                    //     Debug.Log(string.Join( " " , ms.ToArray().Select(x => x.ToString("X2"))));
                    //     Debug.Log(ms.ToArray().Length);
                    // }
                    return loadedData;
                }
            }
        }

        /// <summary>
        /// A shortcut for <see cref="ProtoFromStream{P}(Stream, byte[])"> then <see cref="StreamToFile(MemoryStream, string, string)">.
        /// </summary>
        public static void ProtoToFile<P>(P save, byte[] key, string saveFolderAbsolute, string fileNameWithExtension)
            where P : IMessage<P>, new()
            => StreamToFile(ProtoToStream(save, key), saveFolderAbsolute, fileNameWithExtension);


        /// <summary>
        /// The <paramref name="memStream"> probably should came from <see cref="ProtoToStream{P}(P, byte[])">.
        /// Automatically dispose the stream after it is done.
        /// </summary>
        public static void StreamToFile(MemoryStream memStream, string saveFolderAbsolute, string fileNameWithExtension)
        {
            Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER", "yes");

            //Debug.Log("Saved : " + Application.persistentDataPath);
            using (FileStream file = File.Create($"{saveFolderAbsolute}/{fileNameWithExtension}"))
            using (memStream)
            {
                byte[] bytes = memStream.ToArray();
                file.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Applies basic AES encryption with the provided <paramref name="key">, with generated IV
        /// pasted in front of cipher text, before writing to the disk.
        /// </summary>
        /// <typeparam name="P">Your protobuf type here.</typeparam>
        public static MemoryStream ProtoToStream<P>(P save, byte[] key)
        where P : IMessage<P>, new()
        {
            MemoryStream memStream = new MemoryStream();
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            aes.Key = key;
            aes.GenerateIV();
            // Debug.Log(aes.BlockSize);
            // Debug.Log(aes.KeySize);
            // Debug.Log(aes.FeedbackSize);
            // Debug.Log(aes.Padding);

            //Simply paste the initialization vector in front of cipher text.
            memStream.Write(aes.IV, 0, 16);

            // Debug.Log("Key " + BitConverter.ToString(aes.Key));
            // Debug.Log("IV " + BitConverter.ToString(aes.IV));
            // using (MemoryStream ms = new MemoryStream())
            // {
            //     using (Google.Protobuf.CodedOutputStream cos = new CodedOutputStream(ms))
            //     {
            //         save.WriteTo(cos);
            //     }
            //     Debug.Log(save);
            //     var a = ms.ToArray();
            //     Debug.Log(string.Join(" ", a.Select(x => x.ToString("X2"))));
            //     Debug.Log(a.Length);

            //     using (MemoryStream ms2 = new MemoryStream())
            //     {
            //         using (var cryptoStream = new CryptoStream(ms2, aes.CreateEncryptor(), CryptoStreamMode.Write))
            //         {
            //             cryptoStream.Write(a, 0, a.Length);
            //         }
            //         Debug.Log(string.Join(" ", ms2.ToArray().Select(x => x.ToString("X2"))));
            //         Debug.Log(ms2.ToArray().Length);
            //     }
            // }

            using (var cryptoStream = new CryptoStream(memStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                using (Google.Protobuf.CodedOutputStream cos = new Google.Protobuf.CodedOutputStream(cryptoStream))
                {
                    save.WriteTo(cos);
                }
            }
            // Debug.Log(string.Join( " " , memStream.ToArray().Select(x => x.ToString("X2"))));
            // Debug.Log(memStream.ToArray().Length);
            return memStream;
        }

        public static PROTO ProtoFromFile<PROTO>(byte[] key, string loadFolderAbsolute, string fileNameWithExtension)
        where PROTO : IMessage<PROTO>, new()
        {
            string path = $"{loadFolderAbsolute}/{fileNameWithExtension}";
            //Debug.Log($"Loading from {path}");
            if (File.Exists(path))
            {
                using (FileStream fileStream = File.Open(path, FileMode.Open))
                {
                    PROTO loaded = ProtoFromStream<PROTO>(fileStream, key);
                    return loaded;
                }
            }
            else
            {
                throw new FileNotFoundException($"Save file not found at path {path}");
            }
        }

#if UNITY_EDITOR

        /// <summary>
        /// Useful in unit testing. You could have a sample of old version saves from player and test your compatibility with them.
        /// Or just a way to setup the test for specific scenario you want to check out.
        /// 
        /// Do not include `Assets` or leading slash in the <paramref name="path">.
        /// </summary>
        public static PROTO ProtoFromProject<PROTO>(byte[] key, string path, string nameWithExtension) where PROTO : IMessage<PROTO>, new()
            => ProtoFromFile<PROTO>(key, $"{Application.dataPath}/{path}", nameWithExtension);
#endif
    }
}