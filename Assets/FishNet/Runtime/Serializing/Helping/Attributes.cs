using FishNet.Utility.Constant;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Serializing.Helping
{
    internal class CodegenMakePublicAttribute : Attribute { }
    public class CodegenExcludeAttribute : Attribute { }
    internal class CodegenIncludeInternalAttribute : Attribute { }

}