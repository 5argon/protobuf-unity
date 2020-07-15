using System.IO;
using UnityEditor;
using UnityEngine;

namespace E7.Protobuf
{
    internal static class ProtoPrefs
    {
        internal static readonly string prefProtocEnable = "ProtobufUnity_Enable";
        internal static readonly string prefProtocExecutable = "ProtobufUnity_ProtocExecutable";
        internal static readonly string prefGrpcPath = "ProtobufUnity_GrpcPath";
        internal static readonly string prefLogError = "ProtobufUnity_LogError";
        internal static readonly string prefLogStandard = "ProtobufUnity_LogStandard";
        internal static bool enabled
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
        internal static bool logError
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

        internal static bool logStandard
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

        internal static string rawExcPath
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

        internal static string excPath
        {
            get
            {
                string ret = EditorPrefs.GetString(prefProtocExecutable, "");
                if (ret.StartsWith(".."))
                    return Path.Combine(Application.dataPath, ret);
                else
                    return ret;
            }
            set
            {
                EditorPrefs.SetString(prefProtocExecutable, value);
            }
        }

        internal static string grpcPath
        {
            get
            {
                string ret = EditorPrefs.GetString(prefGrpcPath, "");
                if (ret.StartsWith(".."))
                    return Path.Combine(Application.dataPath, ret);
                else
                    return ret;
            }
            set
            {
                EditorPrefs.SetString(prefGrpcPath, value);
            }
        }

#if UNITY_2018_3_OR_NEWER
        internal class ProtobufUnitySettingsProvider : SettingsProvider
        {
            public ProtobufUnitySettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope)
            { }

            public override void OnGUI(string searchContext)
            {
                ProtobufPreference();
            }

            [SettingsProvider]
            static SettingsProvider ProtobufPreferenceSettingsProvider()
            {
                return new ProtobufUnitySettingsProvider("Preferences/Protobuf");
            }
        }
#else
        [PreferenceItem("Protobuf")]
#endif
        static void ProtobufPreference()
        {
            EditorGUI.BeginChangeCheck();

            enabled = EditorGUILayout.Toggle(new GUIContent("Enable Protobuf Compilation", ""), enabled);

            EditorGUI.BeginDisabledGroup(!enabled);

            EditorGUILayout.HelpBox(@"On Windows put the path to protoc.exe (e.g. C:\My Dir\protoc.exe), on macOS and Linux you can use ""which protoc"" to find its location. (e.g. /usr/local/bin/protoc)", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path to protoc", GUILayout.Width(100));
            rawExcPath = EditorGUILayout.TextField(rawExcPath, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path to grpc", GUILayout.Width(100));
            grpcPath = EditorGUILayout.TextField(grpcPath, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            logError = EditorGUILayout.Toggle(new GUIContent("Log Error Output", "Log compilation errors from protoc command."), logError);

            logStandard = EditorGUILayout.Toggle(new GUIContent("Log Standard Output", "Log compilation completion messages."), logStandard);

            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Force Compilation")))
            {
                ProtobufUnityCompiler.CompileAllInProject();
            }

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
            }
        }
    }
}