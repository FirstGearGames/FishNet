using TriInspector;
using UnityEngine;

[DeclareBoxGroup("box", Title = "Box")]
[DeclareHorizontalGroup("box/horizontal")]
[DeclareBoxGroup("box/horizontal/one", Title = "One")]
[DeclareBoxGroup("box/horizontal/two", Title = "Two")]
[DeclareBoxGroup("box/three", Title = "Three")]
public class Groups_BoxGroup_StackedSample : ScriptableObject
{
    [Button(ButtonSizes.Large), Group("box/horizontal/one")]
    public void ButtonA()
    {
    }

    [Button(ButtonSizes.Large), Group("box/horizontal/two")]
    public void ButtonB()
    {
    }

    [Button, Group("box/three")]
    public void ButtonC()
    {
    }

    [Button, Group("box/three")]
    public void ButtonD()
    {
    }
}