using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage((AttributeTargets.Field | AttributeTargets.Property))]
    [Conditional("UNITY_EDITOR")]
    public sealed class EnumToggleButtonsAttribute : Attribute
    {
    }
}