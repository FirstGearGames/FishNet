using TriInspector;
using TriInspector.Drawers;
using UnityEditor;
using UnityEngine;

[assembly: RegisterTriAttributeDrawer(typeof(SceneDrawer), TriDrawerOrder.Decorator)]

namespace TriInspector.Drawers
{
    public class SceneDrawer : TriAttributeDrawer<SceneAttribute>
    {
        public override TriExtensionInitializationResult Initialize(TriPropertyDefinition propertyDefinition)
        {
            var type = propertyDefinition.FieldType;
            if (type != typeof(string))
            {
                return "Scene attribute can only be used on field of type int or string";
            }

            return base.Initialize(propertyDefinition);
        }

        public override TriElement CreateElement(TriProperty property, TriElement next)
        {
            return new SceneElement(property);
        }

        private class SceneElement : TriElement
        {
            private readonly TriProperty _property;

            private SceneAsset _sceneAsset;

            public SceneElement(TriProperty property)
            {
                _property = property;
            }

            protected override void OnAttachToPanel()
            {
                base.OnAttachToPanel();

                _property.ValueChanged += OnValueChanged;

                RefreshSceneAsset();
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
                EditorGUI.BeginChangeCheck();

                var asset = EditorGUI.ObjectField(position, _property.DisplayName, _sceneAsset,
                    typeof(SceneAsset), false);

                if (EditorGUI.EndChangeCheck())
                {
                    var path = AssetDatabase.GetAssetPath(asset);
                    _property.SetValue(path);
                }
            }

            private void OnValueChanged(TriProperty property)
            {
                RefreshSceneAsset();
            }

            private void RefreshSceneAsset()
            {
                _sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_property.Value as string);
            }
        }
    }
}