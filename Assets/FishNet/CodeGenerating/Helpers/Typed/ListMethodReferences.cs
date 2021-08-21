using Mono.Cecil;
using System;

namespace FishNet.CodeGenerating.Helping
{


    /// <summary>
    /// References to commonly needed properties or methods within List.
    /// </summary>
    internal class ListMethodReferences
    {
        public Type ListType;
        public MethodReference Item_MethodRef;
        public MethodReference Add_MethodRef;

        public ListMethodReferences(Type lstType, MethodReference item_MethodRef, MethodReference add_MethodRef)
        {
            ListType = lstType;
            Item_MethodRef = item_MethodRef;
            Add_MethodRef = add_MethodRef;
        }
    }

}
