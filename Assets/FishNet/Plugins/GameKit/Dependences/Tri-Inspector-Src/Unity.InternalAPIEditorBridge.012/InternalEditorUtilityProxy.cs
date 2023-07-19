using UnityEditorInternal;

namespace TriInspectorUnityInternalBridge
{
    internal static class InternalEditorUtilityProxy
    {
        public static bool GetIsInspectorExpanded(UnityEngine.Object obj)
        {
            return InternalEditorUtility.GetIsInspectorExpanded(obj);
        }

        public static void SetIsInspectorExpanded(UnityEngine.Object obj, bool isExpanded)
        {
            InternalEditorUtility.SetIsInspectorExpanded(obj, isExpanded);
        }
    }
}