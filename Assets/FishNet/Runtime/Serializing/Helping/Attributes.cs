using FishNet.Utility.Constant;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Serializing.Helping
{
    /// <summary>
    /// Method or type will be made public by codegen.
    /// </summary>
    internal class CodegenMakePublicAttribute : Attribute { }
    /// <summary>
    /// Type will be excluded from codegen.
    /// </summary>
    public class CodegenExcludeAttribute : Attribute { }
    ///// <summary>
    ///// Type will be included in codegen without needing to have any references.
    ///// </summary>
    //internal class CodegenIncludeAttribute : Attribute { }

}