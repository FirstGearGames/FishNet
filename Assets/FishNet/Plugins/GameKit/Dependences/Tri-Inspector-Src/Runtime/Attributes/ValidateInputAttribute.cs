using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    [Conditional("UNITY_EDITOR")]
    public sealed class ValidateInputAttribute : Attribute
    {
        public string Method { get; }

        public ValidateInputAttribute(string method)
        {
            Method = method;
        }
    }
}