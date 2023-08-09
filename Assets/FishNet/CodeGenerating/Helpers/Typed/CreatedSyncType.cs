using MonoFN.Cecil;

namespace FishNet.CodeGenerating.Helping
{



    internal class CreatedSyncVar
    {
        public readonly TypeDefinition VariableTd;
        public readonly MethodReference GetValueMr;
        public readonly MethodReference SetValueMr;
        public readonly MethodReference SetSyncIndexMr;
        public readonly MethodReference ConstructorMr;
        public readonly GenericInstanceType SyncVarGit;
        public FieldDefinition SyncVarClassFd { get; private set; }

        public MethodReference HookMr;
        public CreatedSyncVar(GenericInstanceType syncVarGit, TypeDefinition variableTd, MethodReference getValueMr, MethodReference setValueMr, MethodReference setSyncIndexMr,MethodReference hookMr,  MethodReference constructorMr)
        {
            SyncVarGit = syncVarGit;
            VariableTd = variableTd;
            GetValueMr = getValueMr;
            SetValueMr = setValueMr;
            SetSyncIndexMr = setSyncIndexMr;
            HookMr = hookMr;
            ConstructorMr = constructorMr;
        }

        public void SetSyncVarClassField(FieldDefinition fd)
        {
            SyncVarClassFd = fd;
        }
    }


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