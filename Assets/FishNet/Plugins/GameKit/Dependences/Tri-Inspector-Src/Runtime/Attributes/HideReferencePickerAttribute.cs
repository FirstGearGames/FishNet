using System;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class HideReferencePickerAttribute : Attribute
    {
    }
}