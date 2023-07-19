using System;
using System.Collections.Generic;

namespace TriInspector
{
    public abstract class TriTypeProcessor
    {
        public abstract void ProcessType(Type type, List<TriPropertyDefinition> properties);
    }
}