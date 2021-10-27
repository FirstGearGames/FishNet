using MonoFN.Cecil;

namespace FishNet.CodeGenerating.Helping
{

    internal class CreatedSyncType
    {
        public TypeDefinition StubClassTypeDefinition;
        public MethodReference GetValueMethodReference;
        public MethodReference SetValueMethodReference;
        public MethodReference GetPreviousClientValueMethodReference;
        public MethodReference ReadMethodReference;
        public MethodReference ConstructorMethodReference;
        public CreatedSyncType(TypeDefinition stubClassTypeDef, MethodReference getMethodRef, MethodReference setMethodRef, MethodReference getPreviousMethodRef, MethodReference readMethodRef, MethodReference constructorMethodRef)
        {
            StubClassTypeDefinition = stubClassTypeDef;
            GetValueMethodReference = getMethodRef;
            SetValueMethodReference = setMethodRef;
            GetPreviousClientValueMethodReference = getPreviousMethodRef;
            ReadMethodReference = readMethodRef;
            ConstructorMethodReference = constructorMethodRef;
        }
    }

}