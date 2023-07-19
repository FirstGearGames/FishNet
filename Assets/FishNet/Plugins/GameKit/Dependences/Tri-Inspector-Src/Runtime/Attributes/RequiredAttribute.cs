using System;

namespace TriInspector
{
    [AttributeUsage((AttributeTargets.Field | AttributeTargets.Property))]
    public class RequiredAttribute : Attribute
    {
        public string Message { get; set; }
    }
}