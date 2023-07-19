using System;
using TriInspector;
using UnityEngine;

public class Styling_LabelTextSample : ScriptableObject
{
    [LabelText("Custom Label")]
    public int val;

    [LabelText("$" + nameof(DynamicLabel))]
    public Vector3 vec;

    public string DynamicLabel => DateTime.Now.ToShortTimeString();
}