using FishNet.Utility.Constant;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Serializing.Helping
{
    /// <summary>
    /// Method is a comparer for a value type.
    /// </summary>
    public class CustomComparerAttribute : Attribute { }
    /// <summary>
    /// Method or type will be made public by codegen.
    /// </summary>
    internal class CodegenMakePublicAttribute : Attribute { }
    /// <summary>
    /// Field or type will be excluded from codegen serialization.
    /// </summary>
    public class CodegenExcludeAttribute : Attribute { }
    /// <summary>
    /// THIS DOES NOT DO ANYTHING AT THIS TIME.
    /// It would do -> Type will be included in codegen serialization.
    /// </summary>
    internal class CodegenIncludeAttribute : Attribute { }

}