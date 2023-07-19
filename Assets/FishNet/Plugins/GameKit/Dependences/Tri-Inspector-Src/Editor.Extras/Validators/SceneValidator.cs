using TriInspector;
using TriInspector.Validators;
using UnityEditor;

[assembly: RegisterTriAttributeValidator(typeof(SceneValidator))]

namespace TriInspector.Validators
{
    public class SceneValidator : TriAttributeValidator<SceneAttribute>
    {
        public override TriValidationResult Validate(TriProperty property)
        {
            if (property.FieldType == typeof(string))
            {
                var value = property.Value;

                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (!property.Comparer.Equals(value, scene.path))
                    {
                        continue;
                    }

                    if (!scene.enabled)
                    {
                        return TriValidationResult.Error($"{value} not in build settings");
                    }

                    return TriValidationResult.Valid;
                }
            }

            return TriValidationResult.Error($"{property.Value} not a valid scene");
        }
    }
}