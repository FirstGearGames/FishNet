using System;
using TriInspector;
using UnityEngine;

[DeclareFoldoutGroup("My Foldout", Expanded = true)]
[DeclareFoldoutGroup("boxed_foldout_struct", Title = "Foldout Struct")]
public class Groups_FoldoutGroupSample : ScriptableObject
{
    [Group("My Foldout")] public string c;
    [Group("My Foldout")] public bool d;

    [Group("boxed_foldout_struct"), InlineProperty, HideLabel]
    public MyStruct boxedStruct;

    public MyStruct defaultStruct;

    [Serializable]
    public struct MyStruct
    {
        public int a;
        public float b;
    }
}