using TriInspector;
using UnityEngine;

[DeclareHorizontalGroup("header")]
[DeclareBoxGroup("header/left", Title = "My Left Box")]
[DeclareVerticalGroup("header/right")]
[DeclareBoxGroup("header/right/top", Title = "My Right Box")]
[DeclareTabGroup("header/right/tabs")]
[DeclareBoxGroup("body", HideTitle = true)]
public class Groups_GroupsSample : ScriptableObject
{
    [Group("header/left")] public bool prop1;
    [Group("header/left")] public int prop2;
    [Group("header/left")] public string prop3;
    [Group("header/left")] public Vector3 prop4;

    [Group("header/right/top")] public string rightProp;

    [Group("body")] public string body1;
    [Group("body")] public string body2;

    [Group("header/right/tabs"), Tab("One")]
    public float tabOne;

    [Group("header/right/tabs"), Tab("Two")]
    public float tabTwo;

    [Group("header/right/tabs"), Tab("Three")]
    public float tabThree;

    [Group("header/right"), Button("Click me!")]
    public void MyButton()
    {
    }
}