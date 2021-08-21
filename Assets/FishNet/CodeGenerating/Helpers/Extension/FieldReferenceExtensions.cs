using Mono.Cecil;


namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class FieldReferenceExtensions
    {


        public static FieldReference MakeHostGenericIfNeeded(this FieldReference fd)
        {
            if (fd.DeclaringType.HasGenericParameters)
            {
                return new FieldReference(fd.Name, fd.FieldType, fd.DeclaringType.Resolve().ConvertToGenericIfNeeded());
            }

            return fd;
        }


    }

}