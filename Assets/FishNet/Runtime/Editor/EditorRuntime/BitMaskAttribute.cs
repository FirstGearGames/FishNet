
using UnityEngine;

namespace FishNet.Utility.Editing
{

    /// <summary>
    /// SOURCE: https://answers.unity.com/questions/1477896/assign-enum-value-from-editorguienumflagsfield.html
    /// </summary>
    public class BitMaskAttribute : PropertyAttribute
    {
        public System.Type propType;
        public BitMaskAttribute(System.Type aType)
        {
            propType = aType;
        }
    }

}