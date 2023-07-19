using System;
using TriInspector;
using UnityEngine;

[DeclareBoxGroup("box", HideTitle = true)]
[DeclareBoxGroup("named_box", Title = "My Box")]
[DeclareBoxGroup("boxed_struct", Title = "Boxed Struct")]
public class Groups_BoxGroup_BasicSample : ScriptableObject
{
    [Group("box")] public int a;
    [Group("box")] public float b;

    [Group("named_box")] public string c;
    [Group("named_box")] public bool d;

    [Group("boxed_struct"), InlineProperty, HideLabel]
    public MyStruct boxedStruct;

    public MyStruct defaultStruct;

    [Serializable]
    public struct MyStruct
    {
        public int a;
        public float b;
    }
}