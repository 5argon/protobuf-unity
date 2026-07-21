using Google.Protobuf;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace E7.Protobuf
{
    /// <summary>
    /// A standalone dockable window that shows a single deserialized protobuf message read-only, using
    /// <see cref="ProtobufFacadeInspector" />. Hand it an <see cref="IMessage" /> — from a decrypted save,
    /// a network response, a test fixture — and it renders the whole tree with a Copy-JSON escape hatch.
    /// The message is held in memory only; a domain reload clears it (the window then shows a placeholder).
    /// </summary>
    public class ProtobufViewerWindow : EditorWindow
    {
        IMessage message;
        string subtitle;

        /// <summary>
        /// Open (or focus) the viewer on <paramref name="message" />. <paramref name="subtitle" /> is an
        /// optional line under the type name — e.g. the file the message was read from.
        /// </summary>
        public static ProtobufViewerWindow Show(IMessage message, string subtitle = null)
        {
            ProtobufViewerWindow window = GetWindow<ProtobufViewerWindow>();
            window.titleContent = new GUIContent("Protobuf Viewer");
            window.minSize = new Vector2(360, 300);
            window.SetMessage(message, subtitle);
            window.Focus();
            return window;
        }

        /// <summary>Swap the displayed message without opening a second window.</summary>
        public void SetMessage(IMessage message, string subtitle = null)
        {
            this.message = message;
            this.subtitle = subtitle;
            Rebuild();
        }

        void CreateGUI() => Rebuild();

        void Rebuild()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            string typeName = message?.Descriptor?.FullName ?? "no message";
            Label header = new Label(typeName);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 13;
            root.Add(header);

            if (!string.IsNullOrEmpty(subtitle))
            {
                Label sub = new Label(subtitle);
                sub.style.opacity = 0.6f;
                sub.style.whiteSpace = WhiteSpace.Normal;
                sub.style.marginBottom = 4;
                root.Add(sub);
            }

            // The viewer scrolls itself and carries its own toolbar (search / expand / Copy JSON).
            VisualElement viewer = ProtobufFacadeInspector.Build(message);
            viewer.style.flexGrow = 1;
            root.Add(viewer);
        }
    }
}
