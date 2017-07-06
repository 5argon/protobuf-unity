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

            EditorGUILayout.HelpBox(@"On Windows put the path to protoc.exe (e.g. C:\My Dir\protoc.exe), on macOS and Linux you can use ""which protoc"" to find its location. (e.g. /usr/local/bin/protoc)",MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path to protoc", GUILayout.Width(100));
            excPath = EditorGUILayout.TextField(excPath,  GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            logError = EditorGUILayout.Toggle(new GUIContent("Log Error Output", "Log compilation errors from protoc command."), logError);

            logStandard = EditorGUILayout.Toggle(new GUIContent("Log Standard Output", "Log compilation completion messages."), logStandard);

            EditorGUILayout.Space();

            if(GUILayout.Button(new GUIContent("Force Compilation")))
            {
                CompileAllInProject();
            }

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
            }
        }

	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string str in importedAssets)
        {
            CompileProtobufAssetPath(str);
        }

        for (int i = 0; i < movedAssets.Length; i++)
        {
            CompileProtobufAssetPath(movedAssets[i]);
        }

        AssetDatabase.Refresh();
    }

    private static void CompileAllInProject()
    {
        if (logStandard)
        {
            UnityEngine.Debug.Log("Protobuf Unity : Compiling all .proto files in the project...");
        }
        string[] protoFiles = Directory.GetFiles(Application.dataPath,"*.proto", SearchOption.AllDirectories);
        foreach(string s in protoFiles)
        {
            if (logStandard)
            {
                UnityEngine.Debug.Log("Protobuf Unity : Compiling " + s);
            }
            CompileProtobufSystemPath(s);
        }
        AssetDatabase.Refresh();
    }

    private static void CompileProtobufAssetPath(string assetPath)
    {
        string systemPath = Directory.GetParent(Application.dataPath) + Path.DirectorySeparatorChar.ToString() + assetPath;
        CompileProtobufSystemPath(systemPath);
    }

    private static void CompileProtobufSystemPath(string systemPath)
    {
        if(enabled == false)
        {
            return;
        }

        if (Path.GetExtension(systemPath) == ".proto")
        {
            
            string outputPath = Path.GetDirectoryName(systemPath);

            const string options = " --csharp_out {0} --proto_path {1}";

            ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = excPath, Arguments = string.Format("\"{0}\"",systemPath) + string.Format(options,outputPath,outputPath)};

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
                if(output != "")
                {
                    UnityEngine.Debug.Log("Protobuf Unity : " + output);
                }
                UnityEngine.Debug.Log("Protobuf Unity : Compiled " + Path.GetFileName(systemPath));
            }

            if (logError && error != "")
            {
                UnityEngine.Debug.LogError("Protobuf Unity : " + error);
            }

        }
    }


}

#endif