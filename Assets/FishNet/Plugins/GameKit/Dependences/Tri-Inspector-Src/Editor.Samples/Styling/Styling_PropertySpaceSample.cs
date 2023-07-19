using TriInspector;
using UnityEngine;

public class Styling_PropertySpaceSample : ScriptableObject
{
    [Space, PropertyOrder(0)]
    public Vector3 vecField;

    [ShowInInspector, PropertyOrder(1)]
    [PropertySpace(SpaceBefore = 10, SpaceAfter = 30)]
    public Rect RectProperty { get; set; }

    [PropertyOrder(2)]
    public bool b;
}