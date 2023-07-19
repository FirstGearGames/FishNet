using System;
using System.Diagnostics;

namespace TriInspector
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    [Conditional("UNITY_EDITOR")]
    public class GroupAttribute : Attribute
    {
        public GroupAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}