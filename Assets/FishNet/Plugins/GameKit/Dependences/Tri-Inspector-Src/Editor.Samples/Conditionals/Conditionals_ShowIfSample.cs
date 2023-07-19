using System.Collections.Generic;
using TriInspector;
using UnityEngine;

public class Conditionals_ShowIfSample : ScriptableObject
{
    public Material material;
    public bool toggle;
    public SomeEnum someEnum;

    [ShowIf(nameof(material), null)]
    public Vector3 showWhenMaterialIsNull;

    [ShowIf(nameof(toggle))]
    public List<Vector3> showWhenToggleIsTrue;

    [ShowIf(nameof(toggle), false)]
    public Vector3 showWhenToggleIsFalse;

    [ShowIf(nameof(someEnum), SomeEnum.Two)]
    public Vector3 showWhenSomeEnumIsTwo;

    public enum SomeEnum { One, Two, Three }
}