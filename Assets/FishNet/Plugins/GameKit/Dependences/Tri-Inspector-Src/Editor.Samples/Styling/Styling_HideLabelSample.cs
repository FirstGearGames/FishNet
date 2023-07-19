using TriInspector;
using UnityEngine;

public class Styling_HideLabelSample : ScriptableObject
{
    [Title("Wide Vector")]
    [HideLabel]
    public Vector3 vector;

    [Title("Wide String")]
    [HideLabel]
    public string str;
}