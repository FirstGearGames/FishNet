using MonoFN.Cecil;


namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class FieldReferenceExtensions
    {

        /// <summary>
        /// Gets a Resolve favoring cached results first.
        /// </summary>
        internal static FieldDefinition CachedResolve(this FieldReference fieldRef)
        {
            return CodegenSession.GeneralHelper.GetFieldReferenceResolve(fieldRef);
        }


        public static FieldReference MakeHostGenericIfNeeded(this FieldReference fd)
        {
            if (fd.DeclaringType.HasGenericParameters)
            {
                return new FieldReference(fd.Name, fd.FieldType, fd.DeclaringType.CachedResolve().ConvertToGenericIfNeeded());
            }

            return fd;
        }


    }

}