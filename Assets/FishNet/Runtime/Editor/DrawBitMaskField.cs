#if UNITY_EDITOR
 
using UnityEngine;
using UnityEditor;

namespace FishNet.Utilities.Editing
{

    /// <summary>
    /// SOURCE: https://answers.unity.com/questions/1477896/assign-enum-value-from-editorguienumflagsfield.html
    /// </summary>
    public static class EditorExtension
    {
        public static int DrawBitMaskField(Rect aPosition, int aMask, System.Type aType, GUIContent aLabel)
        {
            var itemNames = System.Enum.GetNames(aType);
            var itemValues = System.Enum.GetValues(aType) as int[];

            int val = aMask;
            int maskVal = 0;
            for (int i = 0; i < itemValues.Length; i++)
            {
                if (itemValues[i] != 0)
                {
                    if ((val & itemValues[i]) == itemValues[i])
                        maskVal |= 1 << i;
                }
                else if (val == 0)
                    maskVal |= 1 << i;
            }
            int newMaskVal = EditorGUI.MaskField(aPosition, aLabel, maskVal, itemNames);
            int changes = maskVal ^ newMaskVal;

            for (int i = 0; i < itemValues.Length; i++)
            {
                if ((changes & (1 << i)) != 0)
                {
                    if ((newMaskVal & (1 << i)) != 0)
                    {
                        if (itemValues[i] == 0)
                        {
                            val = 0;
                            break;
                        }
                        else
                            val |= itemValues[i];
                    }
                    else
                    {
                        val &= ~itemValues[i];
                    }
                }
            }
            return val;
        }
    }

    [CustomPropertyDrawer(typeof(BitMaskAttribute))]
    public class EnumBitMaskPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            var typeAttr = attribute as BitMaskAttribute;
            // Add the actual int value behind the field name
            label.text = label.text + " (" + prop.intValue + ")";
            prop.intValue = EditorExtension.DrawBitMaskField(position, prop.intValue, typeAttr.propType, label);
        }
    }

}

#endif