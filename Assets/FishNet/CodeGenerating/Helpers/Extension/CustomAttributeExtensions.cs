using MonoFN.Cecil;
using System.Linq;

namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class CustomAttributeExtensions
    {
        /// <summary>
        /// Finds a field within an attribute.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="customAttr"></param>
        /// <param name="field"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal static T GetField<T>(this CustomAttribute customAttr, string field, T defaultValue)
        {
            foreach (CustomAttributeNamedArgument customField in customAttr.Fields)
            {
                if (customField.Name == field)
                {
                    return (T)customField.Argument.Value;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Returns if any of the attributes match IAtrribute.
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="attributeProvider"></param>
        /// <returns></returns>
        internal static bool HasCustomAttribute<TAttribute>(this ICustomAttributeProvider attributeProvider)
        {
            return attributeProvider.CustomAttributes.Any(attr => attr.AttributeType.Is<TAttribute>());
        }

        /// <summary>
        /// Returns if ca is of type target.
        /// </summary>
        /// <param name="ca"></param>
        /// <param name="targetFullName"></param>
        /// <returns></returns>
        internal static bool Is(this CustomAttribute ca, string targetFullName)
        {
            return ca.AttributeType.FullName == targetFullName;
        }
    }


}