using TriInspector;
using UnityEngine;

[DeclareTabGroup("tabs")]
public class Groups_TabGroupSample : ScriptableObject
{
    [Group("tabs"), Tab("One")] public int a;
    [Group("tabs"), Tab("One")] public float b;
    [Group("tabs"), Tab("Two")] public bool c;
    [Group("tabs"), Tab("Two")] public Bounds d;
    [Group("tabs"), Tab("Three")] public Vector3 e;
    [Group("tabs"), Tab("Three")] public Rect f;
}