using UnityEngine;
using UnityEngine.UI;

namespace GameKit.Utilities
{

    public static class LayoutGroups
    {
        /// <summary>
        /// Returns how many entries can fit into a GridLayoutGroup
        /// </summary>
        public static int EntriesPerWidth(this GridLayoutGroup lg)
        {
            RectTransform rectTransform = lg.GetComponent<RectTransform>();
            return Mathf.CeilToInt(rectTransform.rect.width / lg.cellSize.x);
        }

    }

}