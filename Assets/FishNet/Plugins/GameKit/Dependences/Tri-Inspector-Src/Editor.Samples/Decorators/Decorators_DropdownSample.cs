using System.Collections.Generic;
using TriInspector;
using UnityEngine;

public class Decorators_DropdownSample : ScriptableObject
{
    [Dropdown(nameof(_intValues))]
    public int intValue = 1;

    [Dropdown(nameof(GetStringValues))]
    public string stringValue;

    [Dropdown(nameof(GetVectorValues))]
    public Vector3 vectorValue;

    private int[] _intValues = {1, 2, 3, 4, 5,};

    private IEnumerable<string> GetStringValues()
    {
        yield return "One";
        yield return "Two";
        yield return "Three";
    }

    private IEnumerable<TriDropdownItem<Vector3>> GetVectorValues()
    {
        return new TriDropdownList<Vector3>
        {
            {"Zero", Vector3.zero},
            {"One/Forward", Vector3.forward},
            {"One/Backward", Vector3.back},
            {"One/Left", Vector3.left},
            {"One/Right", Vector3.right},
        };
    }
}