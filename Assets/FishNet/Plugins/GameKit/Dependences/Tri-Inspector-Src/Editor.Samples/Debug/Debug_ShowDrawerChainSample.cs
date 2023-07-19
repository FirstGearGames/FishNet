using TriInspector;
using UnityEngine;

public class Debug_ShowDrawerChainSample : ScriptableObject
{
    [ShowDrawerChain]
    [Indent]
    [PropertySpace]
    [Title("Custom Title")]
    [GUIColor(1.0f, 0.8f, 0.8f)]
    public Vector3 vec;
}