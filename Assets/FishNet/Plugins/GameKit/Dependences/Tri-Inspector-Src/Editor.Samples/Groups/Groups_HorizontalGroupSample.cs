using TriInspector;
using UnityEngine;

[DeclareHorizontalGroup("vars")]
[DeclareHorizontalGroup("buttons")]
public class Groups_HorizontalGroupSample : ScriptableObject
{
    [Group("vars")] public int a;
    [Group("vars")] public int b;
    [Group("vars")] public int c;

    [Button, Group("buttons")]
    public void ButtonA()
    {
    }

    [Button, Group("buttons")]
    public void ButtonB()
    {
    }
}