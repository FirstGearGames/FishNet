using System;
using JetBrains.Annotations;

namespace TriInspector
{
    public abstract class TriPropertyExtension
    {
        public bool? ApplyOnArrayElement { get; internal set; }

        [PublicAPI]
        public virtual TriExtensionInitializationResult Initialize(TriPropertyDefinition propertyDefinition)
        {
            return TriExtensionInitializationResult.Ok;
        }
    }

    public readonly struct TriExtensionInitializationResult
    {
        public TriExtensionInitializationResult(bool shouldApply, string errorMessage)
        {
            ShouldApply = shouldApply;
            ErrorMessage = errorMessage;
        }

        public bool ShouldApply { get; }
        public string ErrorMessage { get; }
        public bool IsError => ErrorMessage != null;

        [PublicAPI]
        public static TriExtensionInitializationResult Ok => new TriExtensionInitializationResult(true, null);

        [PublicAPI]
        public static TriExtensionInitializationResult Skip => new TriExtensionInitializationResult(false, null);

        [PublicAPI]
        public static TriExtensionInitializationResult Error([NotNull] string errorMessage)
        {
            if (errorMessage == null)
            {
                throw new ArgumentNullException(nameof(errorMessage));
            }

            return new TriExtensionInitializationResult(false, errorMessage);
        }

        [PublicAPI]
        public static implicit operator TriExtensionInitializationResult(string errorMessage)
        {
            return Error(errorMessage);
        }
    }
}