using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace E7.Protobuf
{
    /// <summary>
    /// Renders any deserialized protobuf message as a read-only, three-column tree table (Field / Type /
    /// Value), driven entirely by the message's runtime <see cref="MessageDescriptor" /> — no per-type code.
    /// The Value column is aligned and colour-coded by protobuf type; the view carries a search filter,
    /// column sorting, expand/collapse and Copy-JSON. It inspects a message you already hold in memory; it
    /// does not decrypt, load, or edit anything.
    /// </summary>
    public static class ProtobufFacadeInspector
    {
        /// <summary>
        /// Build a self-contained viewer for <paramref name="message" /> (its own toolbar + scrolling tree),
        /// ready to drop into any editor UI. A null message yields a small placeholder.
        /// </summary>
        public static VisualElement Build(IMessage message)
        {
            ProtobufTreeView view = new ProtobufTreeView();
            view.SetMessage(message);
            return view;
        }

        // ---- descriptor walk → plain node tree ---------------------------------------------------------

        internal static List<ProtoNode> BuildMessageNodes(IMessage message)
        {
            List<ProtoNode> nodes = new List<ProtoNode>();
            foreach (FieldDescriptor field in message.Descriptor.Fields.InFieldNumberOrder())
            {
                nodes.Add(BuildFieldNode(field, field.Accessor.GetValue(message)));
            }
            return nodes;
        }

        static ProtoNode BuildFieldNode(FieldDescriptor field, object value)
        {
            if (field.IsMap) return BuildMapNode(field, value as IDictionary);
            if (field.IsRepeated) return BuildRepeatedNode(field, value as IList);

            if (field.FieldType == FieldType.Message)
            {
                if (value is IMessage nested)
                {
                    return new ProtoNode
                    {
                        Name = field.Name,
                        TypeLabel = field.MessageType.Name,
                        Value = "",
                        Kind = ValueKind.Message,
                        Children = BuildMessageNodes(nested),
                    };
                }
                return new ProtoNode
                {
                    Name = field.Name,
                    TypeLabel = field.MessageType.Name,
                    Value = "<not set>",
                    Kind = ValueKind.Null,
                };
            }
            return ScalarNode(field, value, field.Name);
        }

        static ProtoNode BuildRepeatedNode(FieldDescriptor field, IList list)
        {
            int count = list?.Count ?? 0;
            List<ProtoNode> children = new List<ProtoNode>();
            for (int i = 0; i < count; i++)
            {
                object element = list[i];
                if (field.FieldType == FieldType.Message && element is IMessage nested)
                {
                    children.Add(new ProtoNode
                    {
                        Name = $"[{i}]",
                        TypeLabel = field.MessageType.Name,
                        Value = "",
                        Kind = ValueKind.Message,
                        Children = BuildMessageNodes(nested),
                    });
                }
                else
                {
                    children.Add(ScalarNode(field, element, $"[{i}]"));
                }
            }
            return new ProtoNode
            {
                Name = field.Name,
                TypeLabel = $"repeated {ScalarTypeLabel(field)}",
                Value = count == 0 ? "empty" : count.ToString(),
                Kind = count == 0 ? ValueKind.Empty : ValueKind.Repeated,
                Children = children,
            };
        }

        static ProtoNode BuildMapNode(FieldDescriptor field, IDictionary map)
        {
            int count = map?.Count ?? 0;
            FieldDescriptor keyField = field.MessageType.FindFieldByNumber(1);
            FieldDescriptor valueField = field.MessageType.FindFieldByNumber(2);
            List<ProtoNode> children = new List<ProtoNode>();
            if (map != null)
            {
                foreach (DictionaryEntry entry in map)
                {
                    string key = entry.Key?.ToString() ?? "<null>";
                    if (valueField != null && valueField.FieldType == FieldType.Message && entry.Value is IMessage nested)
                    {
                        children.Add(new ProtoNode
                        {
                            Name = key,
                            TypeLabel = valueField.MessageType.Name,
                            Value = "",
                            Kind = ValueKind.Message,
                            Children = BuildMessageNodes(nested),
                        });
                    }
                    else
                    {
                        children.Add(ScalarNode(valueField, entry.Value, key));
                    }
                }
            }
            string keyLabel = keyField != null ? ScalarTypeLabel(keyField) : "?";
            string valueLabel = valueField != null ? ScalarTypeLabel(valueField) : "?";
            return new ProtoNode
            {
                Name = field.Name,
                TypeLabel = $"map<{keyLabel}, {valueLabel}>",
                Value = count == 0 ? "empty" : count.ToString(),
                Kind = count == 0 ? ValueKind.Empty : ValueKind.Map,
                Children = children,
            };
        }

        // A leaf value for a singular / element / map-value slot, using the given descriptor for its type.
        static ProtoNode ScalarNode(FieldDescriptor field, object value, string name)
        {
            ProtoNode node = new ProtoNode { Name = name, TypeLabel = field != null ? ScalarTypeLabel(field) : "?" };
            FieldType type = field != null ? field.FieldType : FieldType.String;

            if (value == null)
            {
                node.Value = "<null>";
                node.Kind = ValueKind.Null;
                return node;
            }

            switch (type)
            {
                case FieldType.Bool:
                    node.Kind = ValueKind.Bool;
                    node.Bool = (bool)value;
                    node.Value = node.Bool ? "true" : "false";
                    break;
                case FieldType.Enum:
                    int number = Convert.ToInt32(value);
                    EnumValueDescriptor named = field.EnumType.FindValueByNumber(number);
                    node.Kind = ValueKind.Enum;
                    node.Value = named != null ? $"{named.Name} ({number})" : number.ToString();
                    break;
                case FieldType.Bytes:
                    node.Kind = ValueKind.Bytes;
                    if (value is ByteString bytes)
                    {
                        string preview = bytes.ToBase64();
                        if (preview.Length > 120) preview = preview.Substring(0, 120) + "…";
                        node.Value = bytes.Length == 0 ? "0 bytes" : $"{bytes.Length} bytes · {preview}";
                    }
                    else
                    {
                        node.Value = value.ToString();
                    }
                    break;
                case FieldType.String:
                    node.Kind = ValueKind.String;
                    string text = (string)value;
                    node.Value = text.Length == 0 ? "\"\"" : text;
                    break;
                default:
                    node.Kind = ValueKind.Number;
                    node.Value = Convert.ToString(value, CultureInfo.InvariantCulture);
                    break;
            }
            return node;
        }

        static string ScalarTypeLabel(FieldDescriptor field)
        {
            switch (field.FieldType)
            {
                case FieldType.Enum: return field.EnumType.Name;
                case FieldType.Message: return field.MessageType.Name;
                default: return field.FieldType.ToString().ToLowerInvariant();
            }
        }
    }

    // A rendering-ready snapshot of one protobuf field / element / map entry.
    enum ValueKind { Message, Repeated, Map, Number, Bool, String, Enum, Bytes, Null, Empty }

    sealed class ProtoNode
    {
        public string Name;
        public string TypeLabel;
        public string Value;
        public ValueKind Kind;
        public bool Bool;
        public List<ProtoNode> Children;
    }
}
