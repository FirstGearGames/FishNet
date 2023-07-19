using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    [Conditional("UNITY_EDITOR")]
    public sealed class IndentAttribute : Attribute
    {
        public int Indent { get; }

        public IndentAttribute() : this(1)
        {
        }

        public IndentAttribute(int indent)
        {
            Indent = indent;
        }
    }
}