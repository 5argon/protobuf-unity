using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Google.Protobuf;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace E7.Protobuf
{
    /// <summary>
    /// The three-column tree table behind <see cref="ProtobufFacadeInspector.Build" />: a toolbar (search,
    /// expand/collapse, Copy JSON) over a <see cref="MultiColumnTreeView" /> whose Field column expands, Type
    /// column names the protobuf type, and Value column is colour-coded and column-sortable.
    /// </summary>
    sealed class ProtobufTreeView : VisualElement
    {
        readonly MultiColumnTreeView tree;
        IMessage message;
        List<ProtoNode> master = new List<ProtoNode>();
        string query = "";
        string sortColumn;
        bool sortDescending;
        int nextId;

        public ProtobufTreeView()
        {
            style.flexGrow = 1;

            Toolbar toolbar = new Toolbar();

            ToolbarSearchField search = new ToolbarSearchField();
            search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(evt =>
            {
                query = evt.newValue ?? "";
                Refresh();
            });
            toolbar.Add(search);

            toolbar.Add(new ToolbarButton(() => tree.ExpandAll()) { text = "Expand" });
            toolbar.Add(new ToolbarButton(() => tree.CollapseAll()) { text = "Collapse" });
            toolbar.Add(new ToolbarButton(CopyJson) { text = "Copy JSON" });
            Add(toolbar);

            tree = new MultiColumnTreeView();
            tree.style.flexGrow = 1;
            tree.columns.Add(TextColumn("name", "Field", 220, stretch: true, bindText: n => n.Name, kind: ColumnKind.Name));
            tree.columns.Add(TextColumn("type", "Type", 170, stretch: false, bindText: n => n.TypeLabel, kind: ColumnKind.Type));
            tree.columns.Add(TextColumn("value", "Value", 260, stretch: true, bindText: n => n.Value, kind: ColumnKind.Value));

            tree.sortingMode = ColumnSortingMode.Custom;
            tree.columnSortingChanged += OnColumnSortingChanged;
            Add(tree);
        }

        public void SetMessage(IMessage message)
        {
            this.message = message;
            master = message != null ? ProtobufFacadeInspector.BuildMessageNodes(message) : new List<ProtoNode>();
            Refresh();
        }

        enum ColumnKind { Name, Type, Value }

        Column TextColumn(string name, string title, float width, bool stretch, Func<ProtoNode, string> bindText, ColumnKind kind)
        {
            return new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 80,
                stretchable = stretch,
                sortable = true,
                makeCell = () =>
                {
                    Label label = new Label();
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                    label.style.height = Length.Percent(100);
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    if (kind == ColumnKind.Name) label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    if (kind == ColumnKind.Type) label.style.color = Muted;
                    return label;
                },
                bindCell = (element, index) =>
                {
                    ProtoNode node = tree.GetItemDataForIndex<ProtoNode>(index);
                    Label label = (Label)element;
                    label.text = node != null ? bindText(node) : "";
                    label.tooltip = label.text;
                    if (kind == ColumnKind.Value && node != null) label.style.color = ColorFor(node);
                },
            };
        }

        // ---- data pipeline: master → filter → sort → tree items ---------------------------------------

        void Refresh()
        {
            IEnumerable<ProtoNode> working = string.IsNullOrEmpty(query)
                ? master.Select(Clone)
                : master.Select(n => FilterCopy(n, query)).Where(n => n != null);

            List<ProtoNode> roots = working.ToList();
            if (!string.IsNullOrEmpty(sortColumn)) SortRecursive(roots);

            nextId = 0;
            List<TreeViewItemData<ProtoNode>> items = roots.Select(ToItem).ToList();
            tree.SetRootItems(items);
            tree.Rebuild();
            tree.ExpandAll();
        }

        TreeViewItemData<ProtoNode> ToItem(ProtoNode node)
        {
            int id = nextId++;
            List<TreeViewItemData<ProtoNode>> children = null;
            if (node.Children != null && node.Children.Count > 0)
            {
                children = node.Children.Select(ToItem).ToList();
            }
            return new TreeViewItemData<ProtoNode>(id, node, children);
        }

        static ProtoNode Clone(ProtoNode node)
        {
            return new ProtoNode
            {
                Name = node.Name,
                TypeLabel = node.TypeLabel,
                Value = node.Value,
                Kind = node.Kind,
                Bool = node.Bool,
                Children = node.Children?.Select(Clone).ToList(),
            };
        }

        // Keep a node when it matches, or any descendant does (so the path to a match stays visible).
        static ProtoNode FilterCopy(ProtoNode node, string q)
        {
            bool selfMatch =
                Contains(node.Name, q) || Contains(node.Value, q) || Contains(node.TypeLabel, q);

            List<ProtoNode> keptChildren = null;
            if (node.Children != null)
            {
                keptChildren = new List<ProtoNode>();
                foreach (ProtoNode child in node.Children)
                {
                    ProtoNode kept = FilterCopy(child, q);
                    if (kept != null) keptChildren.Add(kept);
                }
            }

            if (!selfMatch && (keptChildren == null || keptChildren.Count == 0)) return null;
            ProtoNode copy = Clone(node);
            copy.Children = node.Children == null ? null : keptChildren;
            return copy;
        }

        static bool Contains(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        void SortRecursive(List<ProtoNode> nodes)
        {
            nodes.Sort(Compare);
            foreach (ProtoNode node in nodes)
            {
                if (node.Children != null) SortRecursive(node.Children);
            }
        }

        int Compare(ProtoNode a, ProtoNode b)
        {
            int result;
            switch (sortColumn)
            {
                case "type":
                    result = string.Compare(a.TypeLabel, b.TypeLabel, StringComparison.OrdinalIgnoreCase);
                    break;
                case "value":
                    result = CompareValue(a, b);
                    break;
                default:
                    result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    break;
            }
            return sortDescending ? -result : result;
        }

        static int CompareValue(ProtoNode a, ProtoNode b)
        {
            if (a.Kind == ValueKind.Number && b.Kind == ValueKind.Number
                && double.TryParse(a.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double da)
                && double.TryParse(b.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double db))
            {
                return da.CompareTo(db);
            }
            return string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase);
        }

        void OnColumnSortingChanged()
        {
            SortColumnDescription sorted = tree.sortedColumns.FirstOrDefault();
            if (sorted == null)
            {
                sortColumn = null;
            }
            else
            {
                sortColumn = sorted.columnName;
                sortDescending = sorted.direction == SortDirection.Descending;
            }
            Refresh();
        }

        void CopyJson()
        {
            if (message == null) return;
            try
            {
                JsonFormatter formatter = new JsonFormatter(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));
                EditorGUIUtility.systemCopyBuffer = formatter.Format(message);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not format protobuf message as JSON: {e.Message}");
            }
        }

        // ---- colours (tuned to read on the dark editor skin) ------------------------------------------

        static readonly Color Muted = new Color(0.55f, 0.55f, 0.55f);

        static Color ColorFor(ProtoNode node)
        {
            switch (node.Kind)
            {
                case ValueKind.Bool: return node.Bool ? new Color(0.45f, 0.80f, 0.45f) : new Color(0.86f, 0.52f, 0.38f);
                case ValueKind.Number: return new Color(0.44f, 0.66f, 0.94f);
                case ValueKind.String: return new Color(0.83f, 0.66f, 0.44f);
                case ValueKind.Enum: return new Color(0.68f, 0.56f, 0.92f);
                case ValueKind.Bytes: return new Color(0.60f, 0.72f, 0.72f);
                default: return Muted;
            }
        }
    }
}
