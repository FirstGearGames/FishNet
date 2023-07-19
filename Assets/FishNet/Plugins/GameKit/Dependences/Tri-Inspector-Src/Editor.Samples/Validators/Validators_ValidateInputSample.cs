using TriInspector;
using UnityEngine;

public class Validators_ValidateInputSample : ScriptableObject
{
    [ValidateInput(nameof(ValidateTexture))]
    public Texture tex;

    private TriValidationResult ValidateTexture()
    {
        if (tex == null) return TriValidationResult.Error("Tex is null");
        if (!tex.isReadable) return TriValidationResult.Warning("Tex must be readable");
        return TriValidationResult.Valid;
    }
}