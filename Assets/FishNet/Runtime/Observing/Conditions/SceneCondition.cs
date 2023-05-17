using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Component.Observing
{

    /// <summary>
    /// When this observer condition is placed on an object, a client must be within the same scene to view the object.
    /// </summary>
    [CreateAssetMenu(menuName = "FishNet/Observers/Scene Condition", fileName = "New Scene Condition")]
    public class SceneCondition : ObserverCondition
    {
        #region Serialized.
        ///// <summary>
        ///// True to synchronize which scene the object was spawned in to clients. When true this object will be moved to the clients equivelant of the scene it was spawned in on the server. This setting does not continously move this object to the same scene.
        ///// </summary>
        //[Tooltip("True to synchronize which scene the object was spawned in to clients. When true this object will be moved to the clients equivelant of the scene it was spawned in on the server. This setting does not continously move this object to the same scene.")]
        //[SerializeField]
        //private bool _synchronizeScene;
        #endregion

        public void ConditionConstructor()
        {
            //_synchronizeScene = synchronizeScene;
        }

        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection">Connection which the condition is being checked for.</param>
        /// <param name="currentlyAdded">True if the connection currently has visibility of this object.</param>
        /// <param name="notProcessed">True if the condition was not processed. This can be used to skip processing for performance. While output as true this condition result assumes the previous ConditionMet value.</param>
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            /* When there is no owner only then is the gameobject
             * scene checked. That's the only way to know at this point. */
            return connection.Scenes.Contains(base.NetworkObject.gameObject.scene);
        }

        /// <summary>
        /// How a condition is handled.
        /// </summary>
        /// <returns></returns>
        public override ObserverConditionType GetConditionType() => ObserverConditionType.Normal;


        /// <summary>
        /// Clones referenced ObserverCondition. This must be populated with your conditions settings.
        /// </summary>
        /// <returns></returns>
        public override ObserverCondition Clone()
        {
            SceneCondition copy = ScriptableObject.CreateInstance<SceneCondition>();
            //copy.ConditionConstructor(_synchronizeScene);
            copy.ConditionConstructor();
            return copy;
        }

    }
}
