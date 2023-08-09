#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameKit.Dependencies.Inspectors
{

    /// <summary>
    /// Based on: https://forum.unity.com/threads/draw-a-field-only-if-a-condition-is-met.448855/
    /// </summary>
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfPropertyDrawer : PropertyDrawer
    {
        #region Fields

        // Reference to the attribute on the property.
        ShowIfAttribute drawIf;

        // Field that is being compared.
        SerializedProperty comparedField;

        #endregion


        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!ShowMe(property) && drawIf.disablingType == ShowIfAttribute.DisablingType.DontDraw)
            {
                return -EditorGUIUtility.standardVerticalSpacing;
            }
            else
            {
                if (property.propertyType == SerializedPropertyType.Generic)
                {
                    int numChildren = 0;
                    float totalHeight = 0.0f;

                    IEnumerator children = property.GetEnumerator();
                    HashSet<SerializedProperty> drawnprops = new HashSet<SerializedProperty>();

                    while (children.MoveNext())
                    {
                        SerializedProperty child = children.Current as SerializedProperty;
                        if (drawnprops.Contains(child))
                        {
                            continue;
                        }
                        drawnprops.Add(child);

                        GUIContent childLabel = new GUIContent(child.displayName);

                        totalHeight += EditorGUI.GetPropertyHeight(child, childLabel) + EditorGUIUtility.standardVerticalSpacing;
                        numChildren++;
                    }

                    // Remove extra space at end, (we only want spaces between items)
                    totalHeight -= EditorGUIUtility.standardVerticalSpacing;

                    return totalHeight;
                }

                return EditorGUI.GetPropertyHeight(property, label);
            }
        }


        /// <summary>
        /// Errors default to showing the property.
        /// </summary>
        private bool ShowMe(SerializedProperty property)
        {
            drawIf = attribute as ShowIfAttribute;
            // Replace propertyname to the value from the parameter
            string path = property.propertyPath.Contains(".") ? System.IO.Path.ChangeExtension(property.propertyPath, drawIf.comparedPropertyName) : drawIf.comparedPropertyName;

            comparedField = property.serializedObject.FindProperty(path);

            if (comparedField == null)
            {
                Debug.LogError("Cannot find property with name: " + path);
                return true;
            }

            // get the value & compare based on types
            switch (comparedField.type)
            { // Possible extend cases to support your own type
                case "bool":
                    return comparedField.boolValue.Equals(drawIf.comparedValue);
                case "Enum":
                    return comparedField.enumValueIndex.Equals((int)drawIf.comparedValue);
                default:
                    Debug.LogError("Error: " + comparedField.type + " is not supported of " + path);
                    return true;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // If the condition is met, simply draw the field.
            if (ShowMe(property))
            {
                EditorGUI.PropertyField(position, property);
            } //...check if the disabling type is read only. If it is, draw it disabled
            else if (drawIf.disablingType == ShowIfAttribute.DisablingType.ReadOnly)
            {
                GUI.enabled = false;
                EditorGUI.PropertyField(position, property);
                GUI.enabled = true;
            }
        }

    }


}
#endif