using System;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

public class Collections_TableListSample : ScriptableObject
{
    [TableList(Draggable = true,
        HideAddButton = false,
        HideRemoveButton = false,
        AlwaysExpanded = false)]
    public List<TableItem> table;

    [Serializable]
    public class TableItem
    {
        [Required]
        public Texture icon;

        public string description;

        [Group("Combined"), LabelWidth(16)]
        public string A, B, C;

        [Button, Group("Actions")]
        public void Test1()
        {
        }

        [Button, Group("Actions")]
        public void Test2()
        {
        }
    }
}