using System;
using System.Reflection;
using UnityEditorInternal;
using UnityEngine;

namespace TriInspectorUnityInternalBridge
{
    internal static class ReorderableListProxy
    {
#if !UNITY_2021_3_OR_NEWER
        private static readonly MethodInfo ClearCacheMethod;
#endif

        private static ReorderableList.Defaults _defaultBehaviours;

        // ReSharper disable once InconsistentNaming
        public static ReorderableList.Defaults defaultBehaviours
        {
            get
            {
                if (_defaultBehaviours == null)
                {
                    _defaultBehaviours = new ReorderableList.Defaults();
                }

                return _defaultBehaviours;
            }
        }

        static ReorderableListProxy()
        {
#if !UNITY_2021_3_OR_NEWER
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            ClearCacheMethod = typeof(ReorderableList).GetMethod("InvalidateCacheRecursive", flags) ??
                               typeof(ReorderableList).GetMethod("ClearCacheRecursive", flags);
#endif
        }

        public static void DoListHeader(ReorderableList list, Rect headerRect)
        {
            if (list.showDefaultBackground && Event.current.type == EventType.Repaint)
            {
                defaultBehaviours.DrawHeaderBackground(headerRect);
            }

            headerRect.xMin += 6f;
            headerRect.xMax -= 6f;
            headerRect.height -= 2f;
            headerRect.y += 1;

            list.drawHeaderCallback?.Invoke(headerRect);
        }

        public static void ClearCacheRecursive(ReorderableList list)
        {
#if UNITY_2021_3_OR_NEWER
            list.InvalidateCacheRecursive();
#else
            ClearCacheMethod?.Invoke(list, Array.Empty<object>());
#endif
        }
    }
}