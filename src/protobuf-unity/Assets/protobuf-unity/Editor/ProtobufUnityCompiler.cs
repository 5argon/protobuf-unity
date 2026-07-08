using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;

namespace E7.Protobuf
{
    internal class ProtobufUnityCompiler : AssetPostprocessor
    {
        /// <summary>
        /// Path to the file of all protobuf files in your Unity folder.
        /// </summary>
        static string[] AllProtoFiles
        {
            get
            {
                string[] protoFiles = Directory.GetFiles(Application.dataPath, "*.proto", SearchOption.AllDirectories);
                return protoFiles;
            }
        }

        /// <summary>
        /// A parent folder of all protobuf files found in your Unity project collected together.
        /// This means all .proto files in Unity could import each other freely even if they are far apart.
        /// </summary>
        static string[] IncludePaths
        {
            get
            {
                string[] protoFiles = AllProtoFiles;

                string[] includePaths = new string[protoFiles.Length];
                for (int i = 0; i < protoFiles.Length; i++)
                {
                    string protoFolder = Path.GetDirectoryName(protoFiles[i]);
                    includePaths[i] = protoFolder;
                }
                return includePaths;
            }
        }

        static bool anyChanges = false;
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            anyChanges = false;
            if (ProtoPrefs.enabled == false)
            {
                return;
            }

            string[] includePaths = IncludePaths;
            foreach (string str in importedAssets)
            {
                if (CompileProtobufAssetPath(str, includePaths) == true)
                {
                    anyChanges = true;
                }
            }

            /*
            for (int i = 0; i < movedAssets.Length; i++)
            {
                CompileProtobufAssetPath(movedAssets[i]);
            }
            */

            if (anyChanges)
            {
                UnityEngine.Debug.Log(nameof(ProtobufUnityCompiler));
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Called from Force Compilation button in the prefs.
        /// </summary>
        internal static void CompileAllInProject()
        {
            if (ProtoPrefs.logStandard)
            {
                UnityEngine.Debug.Log("Protobuf Unity : Compiling all .proto files in the project...");
            }

            string[] includePaths = IncludePaths;
            foreach (string s in AllProtoFiles)
            {
                if (ProtoPrefs.logStandard)
                {
                    UnityEngine.Debug.Log("Protobuf Unity : Compiling " + s);
                }
                CompileProtobufSystemPath(s, includePaths);
            }
            UnityEngine.Debug.Log(nameof(ProtobufUnityCompiler));
            AssetDatabase.Refresh();
        }

        private static bool CompileProtobufAssetPath(string assetPath, string[] includePaths)
        {
            string protoFileSystemPath = Directory.GetParent(Application.dataPath) + Path.DirectorySeparatorChar.ToString() + assetPath;
            return CompileProtobufSystemPath(protoFileSystemPath, includePaths);
        }

        private static bool CompileProtobufSystemPath(string protoFileSystemPath, string[] includePaths)
        {
            //Do not compile changes coming from UPM package.
            if (protoFileSystemPath.Contains("Packages/com.e7.protobuf-unity")) return false;

            if (Path.GetExtension(protoFileSystemPath) == ".proto")
            {
                string outputPath = Path.GetDirectoryName(protoFileSystemPath);

                string options = " --csharp_out \"{0}\" ";
                foreach (string s in includePaths)
                {
                    options += string.Format(" --proto_path \"{0}\" ", s);
                }

                // Only add gRPC options when the user has actually set a plugin path.
                if (!string.IsNullOrEmpty(ProtoPrefs.grpcPath))
                    options += $" --grpc_out={outputPath} --plugin=protoc-gen-grpc={ProtoPrefs.grpcPath}";
                //string combinedPath = string.Join(" ", optionFiles.Concat(new string[] { protoFileSystemPath }));

                string finalArguments = string.Format("\"{0}\"", protoFileSystemPath) + string.Format(options, outputPath);

                // C# codegen options (--csharp_opt). Appended after the string.Format above so the
                // option values are never interpreted as format placeholders. A per-folder
                // ProtobufCsharpOptions asset (if any) overrides the global settings.
                string csharpOpt = ResolveCsharpOpt(protoFileSystemPath);
                if (!string.IsNullOrEmpty(csharpOpt))
                    finalArguments += $" --csharp_opt={csharpOpt}";

                if (ProtoPrefs.logStandard)
                {
                    UnityEngine.Debug.Log("Protobuf Unity : Final arguments :\n" + finalArguments);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = ProtoPrefs.excPath, Arguments = finalArguments };

                Process proc = new Process() { StartInfo = startInfo };
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.Start();

                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                if (ProtoPrefs.logStandard)
                {
                    if (output != "")
                    {
                        UnityEngine.Debug.Log("Protobuf Unity : " + output);
                    }
                    UnityEngine.Debug.Log("Protobuf Unity : Compiled " + Path.GetFileName(protoFileSystemPath));
                }

                if (ProtoPrefs.logError && error != "")
                {
                    UnityEngine.Debug.LogError("Protobuf Unity : " + error);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resolves the --csharp_opt value for a proto: the nearest-ancestor
        /// <see cref="ProtobufCsharpOptions"/> asset if one exists, otherwise the global settings.
        /// </summary>
        private static string ResolveCsharpOpt(string protoFileSystemPath)
        {
            string protoAssetPath = ToAssetPath(protoFileSystemPath);
            if (protoAssetPath != null)
            {
                ProtobufCsharpOptions folderOptions = FindNearestFolderOptions(protoAssetPath);
                if (folderOptions != null)
                {
                    return folderOptions.BuildCsharpOpt();
                }
            }
            return ProtoPrefs.BuildCsharpOpt();
        }

        /// <summary>
        /// Converts an absolute file path under the project to an "Assets/..." asset path,
        /// or null if it is not inside the project's Assets folder.
        /// </summary>
        private static string ToAssetPath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/'); // ".../Assets"
            string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length); // ".../"
            return absolute.StartsWith(projectRoot) ? absolute.Substring(projectRoot.Length) : null;
        }

        /// <summary>
        /// Finds the <see cref="ProtobufCsharpOptions"/> asset in the deepest ancestor folder of
        /// <paramref name="protoAssetPath"/> (including its own folder), or null if none applies.
        /// </summary>
        private static ProtobufCsharpOptions FindNearestFolderOptions(string protoAssetPath)
        {
            string protoDir = Path.GetDirectoryName(protoAssetPath).Replace('\\', '/');
            ProtobufCsharpOptions best = null;
            int bestDepth = -1;
            foreach (string guid in AssetDatabase.FindAssets("t:ProtobufCsharpOptions"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string assetDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                bool isAncestorOrSame = protoDir == assetDir || protoDir.StartsWith(assetDir + "/");
                if (isAncestorOrSame && assetDir.Length > bestDepth)
                {
                    ProtobufCsharpOptions loaded = AssetDatabase.LoadAssetAtPath<ProtobufCsharpOptions>(assetPath);
                    if (loaded != null)
                    {
                        best = loaded;
                        bestDepth = assetDir.Length;
                    }
                }
            }
            return best;
        }
    }
}