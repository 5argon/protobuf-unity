using System.Collections.Generic;
using UnityEngine;

namespace E7.Protobuf
{
    /// <summary>
    /// Per-folder override of the C# output options (protoc <c>--csharp_opt</c>).
    /// Place this asset in a folder to make every <c>.proto</c> in that folder — and its
    /// subfolders, until a deeper override is found — compile with these options instead of
    /// the global defaults in Preferences > Protobuf. Each <c>.proto</c> is a separate protoc
    /// run, so different folders can generate with different options.
    /// https://protobuf.dev/reference/csharp/csharp-generated/#compiler_options
    /// </summary>
    [CreateAssetMenu(fileName = "ProtobufCsharpOptions", menuName = "Protobuf Unity/C# Output Options (per folder)")]
    public class ProtobufCsharpOptions : ScriptableObject
    {
        [Tooltip("Generate types as 'internal' instead of 'public' (internal_access).")]
        public bool internalAccess;

        [Tooltip("Add [System.Serializable] to generated message classes (serializable).")]
        public bool serializable;

        [Tooltip("Extension for generated files. Empty = default '.cs'; '.g.cs' is a common choice. (file_extension=)")]
        public string fileExtension = "";

        [Tooltip("Appended verbatim (comma-separated) to --csharp_opt, e.g. base_namespace=Example.")]
        public string extraOptions = "";

        /// <summary>Builds this override's <c>--csharp_opt</c> value.</summary>
        public string BuildCsharpOpt()
            => BuildCsharpOpt(internalAccess, serializable, fileExtension, extraOptions);

        /// <summary>
        /// Shared builder for the <c>--csharp_opt</c> value, used by both the global settings
        /// and per-folder overrides. Returns an empty string when nothing is configured.
        /// </summary>
        public static string BuildCsharpOpt(bool internalAccess, bool serializable, string fileExtension, string extraOptions)
        {
            var parts = new List<string>();
            if (internalAccess) parts.Add("internal_access");
            if (serializable) parts.Add("serializable");
            var ext = (fileExtension ?? "").Trim();
            if (!string.IsNullOrEmpty(ext)) parts.Add($"file_extension={ext}");
            var extra = (extraOptions ?? "").Trim();
            if (!string.IsNullOrEmpty(extra)) parts.Add(extra);
            return string.Join(",", parts);
        }
    }
}
