using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    [Conditional("UNITY_EDITOR")]
    public class DeclareVerticalGroupAttribute : DeclareGroupBaseAttribute
    {
        public DeclareVerticalGroupAttribute(string path) : base(path)
        {
        }
    }
}