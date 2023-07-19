using System;
using System.Collections.Generic;
using TriInspector;
using TriInspector.Drawers;
using TriInspector.Elements;
using TriInspector.Utilities;
using TriInspectorUnityInternalBridge;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

[assembly: RegisterTriAttributeDrawer(typeof(TableListDrawer), TriDrawerOrder.Drawer)]

namespace TriInspector.Drawers
{
    public class TableListDrawer : TriAttributeDrawer<TableListAttribute>
    {
        public override TriExtensionInitializationResult Initialize(TriPropertyDefinition propertyDefinition)
        {
            if (!propertyDefinition.IsArray)
            {
                return "[TableList] valid only on lists";
            }

            return TriExtensionInitializationResult.Ok;
        }

        public override TriElement CreateElement(TriProperty property, TriElement next)
        {
            return new TableElement(property);
        }

        private class TableElement : TriListElement
        {
            private const float FooterExtraSpace = 4;

            private readonly TriProperty _property;
            private readonly TableMultiColumnTreeView _treeView;
            private readonly bool _alwaysExpanded;

            private bool _reloadRequired;
            private bool _heightDirty;
            private bool _isExpanded;
            private int _arraySize;

            public TableElement(TriProperty property) : base(property)
            {
                _property = property;
                _treeView = new TableMultiColumnTreeView(property, this, ListGui)
                {
                    SelectionChangedCallback = SelectionChangedCallback,
                };
                _reloadRequired = true;
            }

            public override bool Update()
            {
                var dirty = base.Update();

                dirty |= ReloadIfRequired();

                if (dirty)
                {
                    _heightDirty = true;
                    _treeView.multiColumnHeader.ResizeToFit();
                }

                return dirty;
            }

            public override float GetHeight(float width)
            {
                _treeView.Width = width;

                if (_heightDirty)
                {
                    _heightDirty = false;
                    _treeView.RefreshHeight();
                }

                var height = 0f;
                height += ListGui.headerHeight;

                if (_property.IsExpanded)
                {
                    height += _treeView.totalHeight;
                    height += ListGui.footerHeight;
                    height += FooterExtraSpace;
                }

                return height;
            }

            public override void OnGUI(Rect position)
            {
                var headerRect = new Rect(position)
                {
                    height = ListGui.headerHeight,
                };
                var elementsRect = new Rect(position)
                {
                    yMin = headerRect.yMax,
                    height = _treeView.totalHeight + FooterExtraSpace,
                };
                var elementsContentRect = new Rect(elementsRect)
                {
                    xMin = elementsRect.xMin + 1,
                    xMax = elementsRect.xMax - 1,
                    yMax = elementsRect.yMax - FooterExtraSpace,
                };
                var footerRect = new Rect(position)
                {
                    yMin = elementsRect.yMax,
                };

                if (!_property.IsExpanded)
                {
                    ReorderableListProxy.DoListHeader(ListGui, headerRect);
                    return;
                }

                if (Event.current.isMouse && Event.current.type == EventType.MouseDrag)
                {
                    _heightDirty = true;
                    _treeView.multiColumnHeader.ResizeToFit();
                }

                if (Event.current.type == EventType.Repaint)
                {
                    ReorderableListProxy.defaultBehaviours.boxBackground.Draw(elementsRect,
                        false, false, false, false);
                }

                ReorderableListProxy.DoListHeader(ListGui, headerRect);

                EditorGUI.BeginChangeCheck();

                _treeView.OnGUI(elementsContentRect);

                if (EditorGUI.EndChangeCheck())
                {
                    _heightDirty = true;
                    _property.PropertyTree.RequestRepaint();
                }

                ReorderableListProxy.defaultBehaviours.DrawFooter(footerRect, ListGui);
            }

            private bool ReloadIfRequired()
            {
                if (!_reloadRequired &&
                    _property.IsExpanded == _isExpanded &&
                    _property.ArrayElementProperties.Count == _arraySize)
                {
                    return false;
                }

                _reloadRequired = false;
                _isExpanded = _property.IsExpanded;
                _arraySize = _property.ArrayElementProperties.Count;

                _treeView.Reload();

                return true;
            }

            protected override TriElement CreateItemElement(TriProperty property)
            {
                return new TableRowElement(property);
            }

            private void SelectionChangedCallback(int index)
            {
                ListGui.index = index;
            }
        }

        [Serializable]
        private class TableMultiColumnTreeView : TreeView
        {
            private readonly TriProperty _property;
            private readonly TriElement _cellElementContainer;
            private readonly ReorderableList _listGui;
            private readonly TableListPropertyOverrideContext _propertyOverrideContext;

            private bool _wasRendered;

            public Action<int> SelectionChangedCallback;

            public TableMultiColumnTreeView(TriProperty property, TriElement container, ReorderableList listGui)
                : base(new TreeViewState(), new TableColumnHeader())
            {
                _property = property;
                _cellElementContainer = container;
                _listGui = listGui;
                _propertyOverrideContext = new TableListPropertyOverrideContext(property);

                showAlternatingRowBackgrounds = true;
                showBorder = false;
                useScrollView = false;

                multiColumnHeader.ResizeToFit();
                multiColumnHeader.visibleColumnsChanged += header => header.ResizeToFit();
            }

            public float Width { get; set; }

            public void RefreshHeight()
            {
                RefreshCustomRowHeights();
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                base.SelectionChanged(selectedIds);

                if (SelectionChangedCallback != null && selectedIds.Count == 1)
                {
                    SelectionChangedCallback.Invoke(selectedIds[0]);
                }
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(0, -1, string.Empty);
                var columns = new List<MultiColumnHeaderState.Column>
                {
                    new MultiColumnHeaderState.Column
                    {
                        width = 16, autoResize = false, canSort = false, allowToggleVisibility = false,
                    },
                };

                if (_property.IsExpanded)
                {
                    for (var index = 0; index < _property.ArrayElementProperties.Count; index++)
                    {
                        var rowChildProperty = _property.ArrayElementProperties[index];
                        root.AddChild(new TableTreeItem(index, rowChildProperty));

                        if (index == 0)
                        {
                            foreach (var kvp in ((TableRowElement) (_cellElementContainer.GetChild(0))).Elements)
                            {
                                columns.Add(new MultiColumnHeaderState.Column
                                {
                                    headerContent = kvp.Value,
                                    headerTextAlignment = TextAlignment.Center,
                                    autoResize = true,
                                    canSort = false,
                                });
                            }
                        }
                    }
                }

                if (root.children == null)
                {
                    root.AddChild(new TableTreeEmptyItem());
                }

                if (multiColumnHeader.state == null ||
                    multiColumnHeader.state.columns.Length == 1)
                {
                    multiColumnHeader.state = new MultiColumnHeaderState(columns.ToArray());
                }

                return root;
            }

            protected override float GetCustomRowHeight(int row, TreeViewItem item)
            {
                if (item is TableTreeEmptyItem)
                {
                    return EditorGUIUtility.singleLineHeight;
                }

                var height = 0f;
                var rowElement = (TableRowElement) _cellElementContainer.GetChild(row);

                foreach (var visibleColumnIndex in multiColumnHeader.state.visibleColumns)
                {
                    var cellWidth = _wasRendered
                        ? multiColumnHeader.GetColumnRect(visibleColumnIndex).width
                        : Width / Mathf.Max(1, multiColumnHeader.state.visibleColumns.Length);

                    var cellHeight = visibleColumnIndex == 0
                        ? EditorGUIUtility.singleLineHeight
                        : rowElement.Elements[visibleColumnIndex - 1].Key.GetHeight(cellWidth);

                    height = Math.Max(height, cellHeight);
                }

                return height + EditorGUIUtility.standardVerticalSpacing * 2;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                if (args.item is TableTreeEmptyItem)
                {
                    base.RowGUI(args);
                    return;
                }

                var rowElement = (TableRowElement) _cellElementContainer.GetChild(args.row);

                for (var i = 0; i < multiColumnHeader.state.visibleColumns.Length; i++)
                {
                    var visibleColumnIndex = multiColumnHeader.state.visibleColumns[i];
                    var rowIndex = args.row;

                    var cellRect = args.GetCellRect(i);
                    cellRect.yMin += EditorGUIUtility.standardVerticalSpacing;

                    if (visibleColumnIndex == 0)
                    {
                        ReorderableListProxy.defaultBehaviours.DrawElementDraggingHandle(cellRect, rowIndex,
                            _listGui.index == rowIndex, _listGui.index == rowIndex, _listGui.draggable);
                        continue;
                    }

                    var cellElement = rowElement.Elements[visibleColumnIndex - 1].Key;
                    cellRect.height = cellElement.GetHeight(cellRect.width);

                    using (TriGuiHelper.PushLabelWidth(EditorGUIUtility.labelWidth / rowElement.ChildrenCount))
                    using (TriPropertyOverrideContext.BeginOverride(_propertyOverrideContext))
                    {
                        cellElement.OnGUI(cellRect);
                    }
                }

                _wasRendered = true;
            }
        }

        public class TableRowElement : TriPropertyCollectionBaseElement
        {
            public TableRowElement(TriProperty property)
            {
                DeclareGroups(property.ValueType);

                Elements = new List<KeyValuePair<TriElement, GUIContent>>();

                if (property.PropertyType == TriPropertyType.Generic)
                {
                    foreach (var childProperty in property.ChildrenProperties)
                    {
                        var oldChildrenCount = ChildrenCount;

                        var props = new TriPropertyElement.Props
                        {
                            forceInline = true,
                        };
                        AddProperty(childProperty, props, out var group);

                        if (oldChildrenCount != ChildrenCount)
                        {
                            var element = GetChild(ChildrenCount - 1);
                            var headerContent = new GUIContent(group ?? childProperty.DisplayName);

                            Elements.Add(new KeyValuePair<TriElement, GUIContent>(element, headerContent));
                        }
                    }
                }
                else
                {
                    var element = new TriPropertyElement(property, new TriPropertyElement.Props
                    {
                        forceInline = true,
                    });
                    var headerContent = new GUIContent("Element");

                    AddChild(element);
                    Elements.Add(new KeyValuePair<TriElement, GUIContent>(element, headerContent));
                }
            }

            public List<KeyValuePair<TriElement, GUIContent>> Elements { get; }
        }

        [Serializable]
        private class TableColumnHeader : MultiColumnHeader
        {
            public TableColumnHeader() : base(null)
            {
                canSort = false;
                height = DefaultGUI.minimumHeight;
            }
        }

        [Serializable]
        private class TableTreeEmptyItem : TreeViewItem
        {
            public TableTreeEmptyItem() : base(0, 0, "Table is Empty")
            {
            }
        }

        [Serializable]
        private class TableTreeItem : TreeViewItem
        {
            public TableTreeItem(int id, TriProperty property) : base(id, 0)
            {
                Property = property;
            }

            public TriProperty Property { get; }
        }

        private class TableListPropertyOverrideContext : TriPropertyOverrideContext
        {
            private readonly TriProperty _grandParentProperty;
            private readonly GUIContent _noneLabel = GUIContent.none;

            public TableListPropertyOverrideContext(TriProperty grandParentProperty)
            {
                _grandParentProperty = grandParentProperty;
            }

            public override bool TryGetDisplayName(TriProperty property, out GUIContent displayName)
            {
                if (property.PropertyType == TriPropertyType.Primitive &&
                    property.Parent?.Parent == _grandParentProperty &&
                    !property.TryGetAttribute(out GroupAttribute _))
                {
                    displayName = _noneLabel;
                    return true;
                }

                displayName = default;
                return false;
            }
        }
    }
}