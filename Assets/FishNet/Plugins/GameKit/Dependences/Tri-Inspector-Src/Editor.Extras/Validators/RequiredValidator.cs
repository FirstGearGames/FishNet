using TriInspector.Validators;
using TriInspector;

[assembly: RegisterTriAttributeValidator(typeof(RequiredValidator), ApplyOnArrayElement = true)]

namespace TriInspector.Validators
{
    public class RequiredValidator : TriAttributeValidator<RequiredAttribute>
    {
        public override TriValidationResult Validate(TriProperty property)
        {
            if (property.FieldType == typeof(string))
            {
                var isNull = string.IsNullOrEmpty((string) property.Value);
                if (isNull)
                {
                    var message = Attribute.Message ?? $"{GetName(property)} is required";
                    return TriValidationResult.Error(message);
                }
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(property.FieldType))
            {
                var isNull = null == (UnityEngine.Object) property.Value;
                if (isNull)
                {
                    var message = Attribute.Message ?? $"{GetName(property)} is required";
                    return TriValidationResult.Error(message);
                }
            }
            else
            {
                return TriValidationResult.Error("RequiredAttribute only valid on Object and String");
            }

            return TriValidationResult.Valid;
        }

        private static string GetName(TriProperty property)
        {
            var name = property.DisplayName;
            if (string.IsNullOrEmpty(name))
            {
                name = property.RawName;
            }

            return name;
        }
    }
}