using TriInspector;
using UnityEngine;

public class Misc_OnValueChangedSample : ScriptableObject
{
    [OnValueChanged(nameof(OnMaterialChanged))]
    public Material mat;

    private void OnMaterialChanged()
    {
        Debug.Log("Material changed!");
    }
}