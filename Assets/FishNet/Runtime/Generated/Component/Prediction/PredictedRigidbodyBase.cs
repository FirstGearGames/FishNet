using FishNet.Documenting;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Prediction
{

    /// <summary>
    /// Base class for predicting rigidbodies for non-owners.
    /// </summary>
    [AddComponentMenu("")]
    [APIExclude]
    public abstract class PredictedRigidbodyBase : MonoBehaviour { }

}