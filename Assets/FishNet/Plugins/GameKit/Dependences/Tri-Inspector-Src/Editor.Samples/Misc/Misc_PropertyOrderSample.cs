using TriInspector;
using UnityEngine;

public class Misc_PropertyOrderSample : ScriptableObject
{
    public float first;

    [PropertyOrder(0)]
    public float second;
}