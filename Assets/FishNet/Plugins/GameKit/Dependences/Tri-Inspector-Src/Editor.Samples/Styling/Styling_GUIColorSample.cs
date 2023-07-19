using TriInspector;
using UnityEngine;

public class Styling_GUIColorSample : ScriptableObject
{
    [GUIColor(0.8f, 1.0f, 0.6f)]
    public Vector3 vec;

    [GUIColor(0.6f, 0.9f, 1.0f)]
    [Button]
    public void BlueButton()
    {
    }

    [GUIColor(1.0f, 0.6f, 0.6f)]
    [Button]
    public void RedButton()
    {
    }
}