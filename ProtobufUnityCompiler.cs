#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Diagnostics;

public class ProtobufUnityCompiler : AssetPostprocessor {

    public static readonly string prefProtocEnable = "ProtobufUnity_Enable";
    public static readonly string prefProtocExecutable = "ProtobufUnity_ProtocExecutable";
    public static readonly string prefLogError = "ProtobufUnity_LogError";
    public static readonly string prefLogStandard = "ProtobufUnity_LogStandard";

    public static bool enabled
    {
        get
        {
            return EditorPrefs.GetBool(prefProtocEnable, true);
        }
        set
        {
            EditorPrefs.SetBool(prefProtocEnable, value);
        }
    }
    public static bool logError
    {
        get
        {
            return EditorPrefs.GetBool(prefLogError, true);
        }
        set
        {
            EditorPrefs.SetBool(prefLogError, value);
        }
    }

    public static bool logStandard
    {
        get
        {
            return EditorPrefs.GetBool(prefLogStandard, false);
        }
        set
        {
            EditorPrefs.SetBool(prefLogStandard, value);
        }
    }

    public static string excPath
    {
        get
        {
            return EditorPrefs.GetString(prefProtocExecutable, "");
        }
        set
        {
            EditorPrefs.SetString(prefProtocExecutable, value);
        }
    }

    [PreferenceItem("Protobuf")]
    static void PreferencesItem()
    {
        EditorGUI.BeginChangeCheck();


        enabled = EditorGUILayout.Toggle(new GUIContent("Enable Protobuf Compilation", ""), enabled);

        EditorGUI.BeginDisabledGroup(!enabled);

        EditorGUILayout.HelpBox(@"On Windows put the path to protoc.exe (e.g. C:\My Dir\protoc.exe), on macOS and Linux you can use ""which protoc"" to find its location. (e.g. /usr/local/bin/protoc)", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Path to protoc", GUILayout.Width(100));
        excPath = EditorGUILayout.TextField(excPath, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        logError = EditorGUILayout.Toggle(new GUIContent("Log Error Output", "Log compilation errors from protoc command."), logError);

        logStandard = EditorGUILayout.Toggle(new GUIContent("Log Standard Output", "Log compilation completion messages."), logStandard);

        EditorGUILayout.Space();

        if (GUILayout.Button(new GUIContent("Force Compilation")))
        {
            CompileAllInProject();
        }

        EditorGUI.EndDisabledGroup();

        if (EditorGUI.EndChangeCheck())
        {
        }
    }

    static string[] AllProtoFiles
    {
        get
        {
            string[] protoFiles = Directory.GetFiles(Application.dataPath, "*.proto", SearchOption.AllDirectories);
            return protoFiles;
        }
    }

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


    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (enabled == false)
        {
            return;
        }


        foreach (string str in importedAssets)
        {
            CompileProtobufAssetPath(str, IncludePaths);
        }

        /*
        for (int i = 0; i < movedAssets.Length; i++)
        {
            CompileProtobufAssetPath(movedAssets[i]);
        }
        */

        AssetDatabase.Refresh();
    }

    private static void CompileAllInProject()
    {
        if (logStandard)
        {
            UnityEngine.Debug.Log("Protobuf Unity : Compiling all .proto files in the project...");
        }


        foreach (string s in AllProtoFiles)
        {
            if (logStandard)
            {
                UnityEngine.Debug.Log("Protobuf Unity : Compiling " + s);
            }
            CompileProtobufSystemPath(s, IncludePaths);
        }
        AssetDatabase.Refresh();
    }

    private static void CompileProtobufAssetPath(string assetPath, string[] includePaths)
    {
        string protoFileSystemPath = Directory.GetParent(Application.dataPath) + Path.DirectorySeparatorChar.ToString() + assetPath;
        CompileProtobufSystemPath(protoFileSystemPath, includePaths);
    }

    private static void CompileProtobufSystemPath(string protoFileSystemPath, string[] includePaths)
    {

        if (Path.GetExtension(protoFileSystemPath) == ".proto")
        {
            string outputPath = Path.GetDirectoryName(protoFileSystemPath);

            string options = " --csharp_out {0} ";
            foreach (string s in includePaths)
            {
                options += string.Format(" --proto_path {0} ", s);
            }

            string finalArguments = string.Format("\"{0}\"", protoFileSystemPath) + string.Format(options, outputPath);

            // if (logStandard)
            // {
            //     UnityEngine.Debug.Log("Protobuf Unity : Arguments debug : " + finalArguments);
            // }

            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = excPath, Arguments =  finalArguments};

            Process proc = new Process() { StartInfo = startInfo };
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (logStandard)
            {
                if (output != "")
                {
                    UnityEngine.Debug.Log("Protobuf Unity : " + output);
                }
                UnityEngine.Debug.Log("Protobuf Unity : Compiled " + Path.GetFileName(protoFileSystemPath));
            }

            if (logError && error != "")
            {
                UnityEngine.Debug.LogError("Protobuf Unity : " + error);
            }

        }
    }


}

#endif
