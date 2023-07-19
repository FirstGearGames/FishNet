using System;
using System.Collections.Generic;
using System.Linq;
using TriInspector.Utilities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace TriInspector.Editor.Samples
{
    internal class TriSamplesWindow : EditorWindow
    {
        private MenuTree _menuTree;
        private SearchField _searchField;

        private ScriptableObject _current;
        private SerializedObject _currentSerializedObject;
        private TriPropertyTree _currentPropertyTree;
        private MonoScript _currentMonoScript;
        private Vector2 _currentScroll;

        [MenuItem("Tools/Tri Inspector/Samples")]
        public static void Open()
        {
            var window = GetWindow<TriSamplesWindow>();
            window.titleContent = new GUIContent("Tri Samples");
            window.Show();
        }

        private void OnEnable()
        {
            _menuTree = new MenuTree(new TreeViewState());
            _menuTree.SelectedTypeChanged += ChangeCurrentSample;

            _searchField = new SearchField();
            _searchField.downOrUpArrowKeyPressed += _menuTree.SetFocusAndEnsureSelectedItem;

            _menuTree.Reload();
        }

        private void OnDisable()
        {
            ChangeCurrentSample(null);
        }

        private void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope(GUILayout.Width(200)))
                {
                    DrawMenu();
                }

                var separatorRect = GUILayoutUtility.GetLastRect();
                separatorRect.xMin = separatorRect.xMax;
                separatorRect.xMax += 1;
                GUI.Box(separatorRect, "");

                using (new GUILayout.VerticalScope())
                {
                    DrawElement();
                }
            }
        }

        private void DrawMenu()
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                GUILayout.Space(5);
                _menuTree.searchString = _searchField.OnToolbarGUI(_menuTree.searchString, GUILayout.ExpandWidth(true));
                GUILayout.Space(5);
            }

            var menuRect = GUILayoutUtility.GetRect(0, 100000, 0, 100000);
            _menuTree.OnGUI(menuRect);
        }

        private void DrawElement()
        {
            if (_currentPropertyTree == null || _currentMonoScript == null)
            {
                return;
            }

            using (var scrollScope = new GUILayout.ScrollViewScope(_currentScroll))
            {
                _currentScroll = scrollScope.scrollPosition;

                using (new GUILayout.VerticalScope(SampleWindowStyles.Padding))
                {
                    GUILayout.Label(_current.name, SampleWindowStyles.HeaderDisplayNameLabel);

                    _currentSerializedObject.UpdateIfRequiredOrScript();
                    _currentPropertyTree.Update();

                    if (_currentPropertyTree.ValidationRequired)
                    {
                        _currentPropertyTree.RunValidation();
                    }

                    GUILayout.Space(10);
                    GUILayout.Label("Preview", EditorStyles.boldLabel);

                    using (TriGuiHelper.PushEditorTarget(_current))
                    using (new GUILayout.VerticalScope(SampleWindowStyles.BoxWithPadding))
                    {
                        var viewWidth = GUILayoutUtility.GetRect(0, 10000, 0, 0).width;
                        _currentPropertyTree.Draw(viewWidth);
                    }

                    if (_currentSerializedObject.ApplyModifiedProperties())
                    {
                        _currentPropertyTree.RequestValidation();
                    }

                    if (_currentPropertyTree.RepaintRequired)
                    {
                        Repaint();
                    }

                    GUILayout.Space(10);
                    GUILayout.Label("Code", EditorStyles.boldLabel);

                    using (new GUILayout.VerticalScope(SampleWindowStyles.BoxWithPadding))
                    {
                        GUILayout.TextField(_currentMonoScript.text);
                    }
                }
            }
        }

        private void ChangeCurrentSample(Type type)
        {
            if (_current != null)
            {
                DestroyImmediate(_current);
                _current = null;
            }

            if (_currentSerializedObject != null)
            {
                _currentSerializedObject.Dispose();
                _currentSerializedObject = null;
            }

            if (_currentPropertyTree != null)
            {
                _currentPropertyTree.Dispose();
                _currentPropertyTree = null;
            }

            _currentScroll = Vector2.zero;

            if (type != null)
            {
                _current = CreateInstance(type);
                _current.name = GetTypeNiceName(type);
                _current.hideFlags = HideFlags.DontSave;

                _currentSerializedObject = new SerializedObject(_current);
                _currentMonoScript = MonoScript.FromScriptableObject(_current);
                _currentPropertyTree = new TriPropertyTreeForSerializedObject(_currentSerializedObject);
            }
        }

        private static string GetTypeNiceName(Type type)
        {
            var name = type.Name;

            if (name.Contains('_'))
            {
                var index = name.IndexOf('_');
                name = name.Substring(index + 1);
            }

            if (name.EndsWith("Sample"))
            {
                name = name.Remove(name.Length - "Sample".Length);
            }

            return name;
        }

        private class MenuTree : TreeView
        {
            private readonly Dictionary<string, GroupItem> _groups = new Dictionary<string, GroupItem>();

            public event Action<Type> SelectedTypeChanged;

            public MenuTree(TreeViewState state) : base(state)
            {
            }

            protected override bool CanMultiSelect(TreeViewItem item)
            {
                return false;
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                base.SelectionChanged(selectedIds);

                var type = selectedIds.Count > 0 && FindItem(selectedIds[0], rootItem) is SampleItem sampleItem
                    ? sampleItem.Type
                    : null;

                SelectedTypeChanged?.Invoke(type);
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(-1, -1);

                var sampleTypes = typeof(TriSamplesWindow).Assembly.GetTypes()
                    .Where(type => type.BaseType == typeof(ScriptableObject))
                    .OrderBy(type => type.Name)
                    .ToList();

                var id = 0;
                foreach (var sampleType in sampleTypes)
                {
                    var group = sampleType.Name.Split('_')[0];

                    if (!_groups.TryGetValue(group, out var groupItem))
                    {
                        _groups[group] = groupItem = new GroupItem(++id, group);

                        root.AddChild(groupItem);
                    }

                    groupItem.AddChild(new SampleItem(++id, sampleType));
                }

                return root;
            }

            private class GroupItem : TreeViewItem
            {
                public GroupItem(int id, string name) : base(id, 0, name)
                {
                }
            }

            private class SampleItem : TreeViewItem
            {
                public Type Type { get; }

                public SampleItem(int id, Type type) : base(id, 1, GetTypeNiceName(type))
                {
                    Type = type;
                }
            }
        }
    }
}