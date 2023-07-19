using TriInspector;
using UnityEngine;

public class Styling_LabelWidthSample : ScriptableObject
{
    public int defaultWidth;

    [LabelWidth(40)]
    public int thin;

    [LabelWidth(300)]
    public int customInspectorVeryLongPropertyName;
}