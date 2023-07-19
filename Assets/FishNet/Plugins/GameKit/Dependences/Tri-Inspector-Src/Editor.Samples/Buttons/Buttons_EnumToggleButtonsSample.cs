using System;
using TriInspector;
using UnityEngine;

public class Buttons_EnumToggleButtonsSample : ScriptableObject
{
    [EnumToggleButtons] public SomeEnum someEnum;
    [EnumToggleButtons] public SomeFlags someFlags;

    public enum SomeEnum
    {
        One,
        Two,
        Three
    }

    [Flags] public enum SomeFlags
    {
        A = 1 << 0,
        B = 1 << 1,
        C = 1 << 2,
        AB = A | B,
        BC = B | C,
    }
}