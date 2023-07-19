using TriInspector;
using UnityEngine;

public class Styling_IndentSample : ScriptableObject
{
    [Title("Custom Indent")]
    [Indent]
    public int a;

    [Indent(2)]
    public int b;

    [Indent(3)]
    public int c;

    [Indent(4)]
    public int d;
}