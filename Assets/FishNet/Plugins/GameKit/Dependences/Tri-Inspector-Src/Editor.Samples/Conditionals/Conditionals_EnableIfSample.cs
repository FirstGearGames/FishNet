using TriInspector;
using UnityEngine;

public class Conditionals_EnableIfSample : ScriptableObject
{
    public bool visible;

    [EnableIf(nameof(visible))]
    public float val;
}