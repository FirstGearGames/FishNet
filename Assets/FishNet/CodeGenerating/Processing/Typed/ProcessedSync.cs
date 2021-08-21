using Mono.Cecil;

namespace FishNet.CodeGenerating.Processing
{

    public class ProcessedSync
    {
        public FieldReference OriginalFieldReference;
        public MethodReference SetMethodReference;
        public MethodReference GetMethodReference;

        public ProcessedSync(FieldReference originalFieldReference, MethodReference setMethodReference, MethodReference getMethodReference)
        {
            OriginalFieldReference = originalFieldReference;
            SetMethodReference = setMethodReference;
            GetMethodReference = getMethodReference;
        }

    }


}