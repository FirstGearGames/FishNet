using MonoFN.Cecil;

namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class FieldReferenceExtensions
    {

        /// <summary>
        /// Gets a Resolve favoring cached results first.
        /// </summary>
        internal static FieldDefinition CachedResolve(this FieldReference fieldRef, CodegenSession session)
        {
            return session.GetClass<GeneralHelper>().GetFieldReferenceResolve(fieldRef);
        }


        public static FieldReference MakeHostGenericIfNeeded(this FieldReference fd, CodegenSession session)
        {
            if (fd.DeclaringType.HasGenericParameters)
            {
                return new FieldReference(fd.Name, fd.FieldType, fd.DeclaringType.CachedResolve(session).ConvertToGenericIfNeeded());
            }

            return fd;
        }


    }

}