using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorUnityInternalBridge;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TriInspector.Utilities
{
    internal static class TriManagedReferenceGui
    {
        public static void DrawTypeSelector(Rect rect, TriProperty property)
        {
            var typeNameContent = new GUIContent(property.ValueType?.Name ?? "[None]");

            if (EditorGUI.DropdownButton(rect, typeNameContent, FocusType.Passive))
            {
                var dropdown = new ReferenceTypeDropDown(property, new AdvancedDropdownState());
                dropdown.Show(rect);

                if (dropdown.CanHideHeader)
                {
                    AdvancedDropdownProxy.SetShowHeader(dropdown, false);
                }

                Event.current.Use();
            }
        }

        private class ReferenceTypeDropDown : AdvancedDropdown
        {
            private readonly TriProperty _property;

            public bool CanHideHeader { get; private set; }

            public ReferenceTypeDropDown(TriProperty property, AdvancedDropdownState state) : base(state)
            {
                _property = property;
                minimumSize = new Vector2(0, 120);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var types = TriReflectionUtilities
                    .AllNonAbstractTypes
                    .Where(type => !typeof(Object).IsAssignableFrom(type))
                    .Where(type => _property.FieldType.IsAssignableFrom(type))
                    .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
                    .ToList();

                var groupByNamespace = types.Count > 20;

                CanHideHeader = !groupByNamespace;

                var root = new ReferenceTypeGroupItem("Type");
                root.AddChild(new ReferenceTypeItem(null));
                root.AddSeparator();

                foreach (var type in types)
                {
                    IEnumerable<string> namespaceEnumerator = groupByNamespace && type.Namespace != null
                        ? type.Namespace.Split('.')
                        : Array.Empty<string>();

                    root.AddTypeChild(type, namespaceEnumerator.GetEnumerator());
                }

                root.Build();

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (!(item is ReferenceTypeItem referenceTypeItem))
                {
                    return;
                }

                if (referenceTypeItem.Type == null)
                {
                    _property.SetValue(null);
                }
                else
                {
                    var instance = Activator.CreateInstance(referenceTypeItem.Type);
                    _property.SetValue(instance);
                }
            }

            private class ReferenceTypeGroupItem : AdvancedDropdownItem
            {
                private static readonly Texture2D ScriptIcon = EditorGUIUtility.FindTexture("cs Script Icon");

                private readonly List<ReferenceTypeItem> _childItems = new List<ReferenceTypeItem>();

                private readonly Dictionary<string, ReferenceTypeGroupItem> _childGroups =
                    new Dictionary<string, ReferenceTypeGroupItem>();

                public ReferenceTypeGroupItem(string name) : base(name)
                {
                }

                public void AddTypeChild(Type type, IEnumerator<string> namespaceRemaining)
                {
                    if (!namespaceRemaining.MoveNext())
                    {
                        _childItems.Add(new ReferenceTypeItem(type, ScriptIcon));
                        return;
                    }

                    var ns = namespaceRemaining.Current ?? "";

                    if (!_childGroups.TryGetValue(ns, out var child))
                    {
                        _childGroups[ns] = child = new ReferenceTypeGroupItem(ns);
                    }

                    child.AddTypeChild(type, namespaceRemaining);
                }

                public void Build()
                {
                    foreach (var child in _childGroups.Values.OrderBy(it => it.name))
                    {
                        AddChild(child);

                        child.Build();
                    }

                    AddSeparator();

                    foreach (var child in _childItems)
                    {
                        AddChild(child);
                    }
                }
            }

            private class ReferenceTypeItem : AdvancedDropdownItem
            {
                public ReferenceTypeItem(Type type, Texture2D preview = null) : base(type?.Name ?? "[None]")
                {
                    Type = type;
                    icon = preview;
                }

                public Type Type { get; }
            }
        }
    }
}