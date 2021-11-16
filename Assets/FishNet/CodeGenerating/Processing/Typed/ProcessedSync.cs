using MonoFN.Cecil;

namespace FishNet.CodeGenerating.Processing
{

    public class ProcessedSync
    {
        public FieldReference OriginalFieldRef;
        public FieldReference GeneratedFieldRef;
        public MethodReference SetMethodRef;
        public MethodReference GetMethodRef;

        public ProcessedSync(FieldReference originalFieldRef,FieldReference generatedFieldRef,  MethodReference setMethodRef, MethodReference getMethodRef)
        {
            OriginalFieldRef = originalFieldRef;
            GeneratedFieldRef = generatedFieldRef;
            SetMethodRef = setMethodRef;
            GetMethodRef = getMethodRef;
        }

    }


}