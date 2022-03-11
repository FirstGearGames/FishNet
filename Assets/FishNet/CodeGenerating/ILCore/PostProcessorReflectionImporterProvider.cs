using MonoFN.Cecil;

namespace FishNet.CodeGenerating.ILCore
{
    internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition moduleDef)
        {
            return new PostProcessorReflectionImporter(moduleDef);
        }
    }
}