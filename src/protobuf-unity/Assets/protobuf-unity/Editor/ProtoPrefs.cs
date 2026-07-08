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
        internal static readonly string prefCsInternalAccess = "ProtobufUnity_CsInternalAccess";
        internal static readonly string prefCsSerializable = "ProtobufUnity_CsSerializable";
        internal static readonly string prefCsFileExtension = "ProtobufUnity_CsFileExtension";
        internal static readonly string prefCsExtraOptions = "ProtobufUnity_CsExtraOptions";
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

        // C# codegen options passed to protoc via --csharp_opt.
        // https://protobuf.dev/reference/csharp/csharp-generated/#compiler_options

        internal static bool csInternalAccess
        {
            get => EditorPrefs.GetBool(prefCsInternalAccess, false);
            set => EditorPrefs.SetBool(prefCsInternalAccess, value);
        }

        internal static bool csSerializable
        {
            get => EditorPrefs.GetBool(prefCsSerializable, false);
            set => EditorPrefs.SetBool(prefCsSerializable, value);
        }

        internal static string csFileExtension
        {
            get => EditorPrefs.GetString(prefCsFileExtension, "");
            set => EditorPrefs.SetString(prefCsFileExtension, value);
        }

        internal static string csExtraOptions
        {
            get => EditorPrefs.GetString(prefCsExtraOptions, "");
            set => EditorPrefs.SetString(prefCsExtraOptions, value);
        }

        /// <summary>
        /// Builds the value for protoc's <c>--csharp_opt</c> flag from the global settings,
        /// or an empty string when nothing is configured. Per-folder overrides live in
        /// <see cref="ProtobufCsharpOptions"/> assets.
        /// </summary>
        internal static string BuildCsharpOpt()
            => ProtobufCsharpOptions.BuildCsharpOpt(csInternalAccess, csSerializable, csFileExtension, csExtraOptions);

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
            EditorGUILayout.LabelField("Global C# output options (--csharp_opt)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Defaults for every .proto. To override per folder, create a \"Protobuf Unity/C# Output Options (per folder)\" asset in that folder — it replaces these for .proto files under it.", MessageType.None);

            csInternalAccess = EditorGUILayout.Toggle(new GUIContent("Global Internal Access", "Generate types with the 'internal' access modifier instead of 'public'."), csInternalAccess);
            csSerializable = EditorGUILayout.Toggle(new GUIContent("Global Serializable", "Add the [System.Serializable] attribute to generated message classes."), csSerializable);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Global File Extension", "Extension for generated files. Empty uses the default '.cs'; a common alternative is '.g.cs' to mark generated code."), GUILayout.Width(140));
            csFileExtension = EditorGUILayout.TextField(csFileExtension, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Global Extra csharp_opt", "Appended verbatim (comma-separated) to --csharp_opt, e.g. base_namespace=Example."), GUILayout.Width(140));
            csExtraOptions = EditorGUILayout.TextField(csExtraOptions, GUILayout.ExpandWidth(true));
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