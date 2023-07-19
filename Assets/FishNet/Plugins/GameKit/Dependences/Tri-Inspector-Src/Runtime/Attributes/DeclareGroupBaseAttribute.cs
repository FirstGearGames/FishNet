using System;

namespace TriInspector
{
    public abstract class DeclareGroupBaseAttribute : Attribute
    {
        protected DeclareGroupBaseAttribute(string path)
        {
            Path = path ?? "None";
        }

        public string Path { get; }
    }
}