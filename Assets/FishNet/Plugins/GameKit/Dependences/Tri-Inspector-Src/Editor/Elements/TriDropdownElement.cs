using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    public class TriDropdownElement : TriElement
    {
        private readonly TriProperty _property;
        private readonly Func<TriProperty, IEnumerable<ITriDropdownItem>> _valuesGetter;

        private string _currentText;

        public TriDropdownElement(TriProperty property, Func<TriProperty, IEnumerable<ITriDropdownItem>> valuesGetter)
        {
            _property = property;
            _valuesGetter = valuesGetter;
        }

        protected override void OnAttachToPanel()
        {
            base.OnAttachToPanel();

            _property.ValueChanged += OnValueChanged;

            RefreshCurrentText();
        }

        protected override void OnDetachFromPanel()
        {
            _property.ValueChanged -= OnValueChanged;

            base.OnDetachFromPanel();
        }

        public override float GetHeight(float width)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position)
        {
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            position = EditorGUI.PrefixLabel(position, controlId, _property.DisplayNameContent);

            if (GUI.Button(position, _currentText, EditorStyles.popup))
            {
                ShowDropdown(position);
            }
        }

        private void OnValueChanged(TriProperty property)
        {
            RefreshCurrentText();
        }

        private void RefreshCurrentText()
        {
            var items = _valuesGetter.Invoke(_property);

            _currentText = items
                .FirstOrDefault(it => _property.Comparer.Equals(it.Value, _property.Value))
                ?.Text ?? "";
        }

        private void ShowDropdown(Rect position)
        {
            var items = _valuesGetter.Invoke(_property);
            var menu = new GenericMenu();

            foreach (var item in items)
            {
                var isOn = _property.Comparer.Equals(item.Value, _property.Value);
                menu.AddItem(new GUIContent(item.Text), isOn, _property.SetValue, item.Value);
            }

            menu.DropDown(position);
        }
    }
}