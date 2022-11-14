using FishNet.CodeGenerating.Extension;
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


        public static FieldReference MakeHostGenericIfNeeded(this FieldDefinition fd, CodegenSession session)
        {
            TypeReference declaringTr = fd.DeclaringType;

            if (declaringTr.HasGenericParameters)
            {
                GenericInstanceType git = declaringTr.MakeGenericInstanceType();
                FieldReference result = new FieldReference(fd.Name, fd.FieldType, git);
                return result;
            }
            else
            {
                return session.ImportReference(fd);
            }
        }


    }

}