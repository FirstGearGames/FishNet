using TriInspector;
using UnityEngine;

public class Conditionals_DisableIfSample : ScriptableObject
{
    public bool visible;

    [DisableIf(nameof(visible))]
    public float val;
}