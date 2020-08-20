using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace E7.Protobuf {
    internal class ProtobufUnityCompiler : AssetPostprocessor {
        /// <summary>
        /// Path to the file of all protobuf files in your Unity folder.
        /// </summary>
        static string[] AllProtoFiles {
            get {
                string[] protoFiles = Directory.GetFiles(Application.dataPath, "*.proto", SearchOption.AllDirectories);
                return protoFiles;
            }
        }

        /// <summary>
        /// A parent folder of all protobuf files found in your Unity project collected together.
        /// This means all .proto files in Unity could import each other freely even if they are far apart.
        /// </summary>
        static string[] IncludePaths {
            get {
                string[] protoFiles = AllProtoFiles;

                string[] includePaths = new string[protoFiles.Length];
                for (int i = 0; i < protoFiles.Length; i++) {
                    string protoFolder = Path.GetDirectoryName(protoFiles[i]);
                    includePaths[i] = protoFolder;
                }

                return includePaths;
            }
        }

        // static bool anyChanges = false;

        // static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
        //     string[] movedFromAssetPaths) {
        //     bool anyRenames = false;
        //     anyChanges = false;
        //     if (ProtoPrefs.enabled == false) {
        //         return;
        //     }
        //
        //     foreach (string str in importedAssets) {
        //         var path = str;
        //         if (ProtoPrefs.uniqueNameToDirectory && Path.GetExtension(str).Equals(".proto")) {
        //             var prefix = Directory.GetParent(str).Name.ToLower();
        //             var fileName = Path.GetFileName(str);
        //             var indexOf = fileName.IndexOf(prefix, StringComparison.Ordinal);
        //             var newName = indexOf == 0 ? fileName : $"{prefix}_{fileName}";
        //     
        //     
        //             if (fileName != newName) {
        //                 path = str.Replace(fileName, newName);
        //                 anyRenames = true;
        //                 AssetDatabase.RenameAsset(str, newName);
        //             }
        //         }
        //     
        //         if (anyRenames) {
        //             AssetDatabase.SaveAssets();
        //         }
        //     
        //         if (CompileProtobufAssetPath(path, IncludePaths) == true) {
        //             anyChanges = true;
        //         }
        //     }
        //
        //     // for (int i = 0; i < movedAssets.Length; i++) {
        //     //     var to = movedAssets[i];
        //     //     var from = movedFromAssetPaths[i];
        //     //     UnityEngine.Debug.Log($"From {from} to {to}");
        //     // }
        //
        //     if (anyChanges) {
        //         UnityEngine.Debug.Log(nameof(ProtobufUnityCompiler));
        //         AssetDatabase.Refresh();
        //     }
        // }

        private static string RenameProto(string str) {
            var path = str;
            if (Path.GetExtension(str).Equals(".proto")) {
                var prefix = Directory.GetParent(str).Name.ToLower();
                var fileName = Path.GetFileName(str);
                var indexOf = fileName.IndexOf(prefix, StringComparison.Ordinal);
                var newName = indexOf == 0 ? fileName : $"{prefix}_{fileName}";

                if (fileName != newName) {
                    path = str.Replace(fileName, newName);
                    var unityPath = str.Remove(0, Application.dataPath.Length - 6);
                    UnityEngine.Debug.Log(unityPath);
                    UnityEngine.Debug.Log(AssetDatabase.RenameAsset(unityPath, newName));
                }
            }

            return path;
        }

        internal static void RemoveGeneratedFiles() {
            var paths = IncludePaths;
            foreach (var path in paths) {
                UnityEngine.Debug.Log(path);
            }
        }


        /// <summary>
        /// Called from Force Compilation button in the prefs.
        /// </summary>
        internal static void CompileAllInProject() {
            if (ProtoPrefs.logStandard) {
                UnityEngine.Debug.Log("Protobuf Unity : Compiling all .proto files in the project...");
            }

            foreach (string s in AllProtoFiles) {
                var path = s;
                var saveAssets = false;
                if (ProtoPrefs.logStandard) {
                    UnityEngine.Debug.Log("Protobuf Unity : Compiling " + s);
                }

                if (ProtoPrefs.uniqueNameToDirectory) {
                    saveAssets = true;
                    path = RenameProto(s);
                    if (ProtoPrefs.logStandard) {
                        UnityEngine.Debug.Log("Protobuf Unity : Renamed " + path);
                    }
                }

                if (saveAssets) {
                    AssetDatabase.SaveAssets();
                }

                CompileProtobufSystemPath(path, IncludePaths);
            }

            UnityEngine.Debug.Log(nameof(ProtobufUnityCompiler));
            AssetDatabase.Refresh();
        }

        private static bool CompileProtobufAssetPath(string assetPath, string[] includePaths) {
            string protoFileSystemPath = Directory.GetParent(Application.dataPath) +
                                         Path.DirectorySeparatorChar.ToString() + assetPath;
            return CompileProtobufSystemPath(protoFileSystemPath, includePaths);
        }

        private static bool CompileProtobufSystemPath(string protoFileSystemPath, string[] includePaths) {
            //Do not compile changes coming from UPM package.
            if (protoFileSystemPath.Contains("Packages/com.e7.protobuf-unity")) return false;

            if (Path.GetExtension(protoFileSystemPath) == ".proto") {
                string outputPath = Path.GetDirectoryName(protoFileSystemPath);

                string options = " --csharp_out \"{0}\" ";
                foreach (string s in includePaths) {
                    options += string.Format(" --proto_path \"{0}\" ", s);
                }

                // Checking if the user has set valid path (there is probably a better way)
                if (ProtoPrefs.grpcPath != "ProtobufUnity_GrpcPath" && ProtoPrefs.grpcPath != string.Empty)
                    options += $" --grpc_out={outputPath} --plugin=protoc-gen-grpc={ProtoPrefs.grpcPath}";
                //string combinedPath = string.Join(" ", optionFiles.Concat(new string[] { protoFileSystemPath }));

                string finalArguments =
                    string.Format("\"{0}\"", protoFileSystemPath) + string.Format(options, outputPath);

                if (ProtoPrefs.logStandard) {
                    UnityEngine.Debug.Log("Protobuf Unity : Final arguments :\n" + finalArguments);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo()
                    {FileName = ProtoPrefs.excPath, Arguments = finalArguments};

                Process proc = new Process() {StartInfo = startInfo};
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (ProtoPrefs.logStandard) {
                    if (output != "") {
                        UnityEngine.Debug.Log("Protobuf Unity : " + output);
                    }

                    UnityEngine.Debug.Log("Protobuf Unity : Compiled " + Path.GetFileName(protoFileSystemPath));
                }

                if (ProtoPrefs.logError && error != "") {
                    UnityEngine.Debug.LogError("Protobuf Unity : " + error);
                }

                return true;
            }

            return false;
        }
    }
}