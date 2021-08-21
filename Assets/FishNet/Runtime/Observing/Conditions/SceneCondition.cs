using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Managing
{

    [CreateAssetMenu(menuName = "FishNet/Observers/Scene Condition", fileName = "New Scene Condition")]
    public class SceneCondition : ObserverCondition
    {
        #region Serialized.
        /// <summary>
        /// True to synchronize which scene the object was spawned in to clients. When true this object will be moved to the clients equivelant of the scene it was spawned in on the server.
        /// </summary>
        [Tooltip("True to synchronize which scene the object was spawned in to clients. When true this object will be moved to the clients equivelant of the scene it was spawned in on the server.")]
        [SerializeField]
        private bool _synchronizeScene = false;
        /// <summary>
        /// True to continuously update network visibility. False to only update on creation or when PerformCheck is called. You may want to use true if this object will move between scenes without using the network scene manager.
        /// </summary>
        [Tooltip("True to continuously update network visibility. False to only update on creation or when PerformCheck is called. You may want to use true if this object will move between scenes without using the network scene manager.")]
        [SerializeField]
        private bool _timed = false;
        #endregion

        public void ConditionConstructor(bool synchronizeScene, bool timed)
        {
            _synchronizeScene = synchronizeScene;
            _timed = timed;
        }

        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection"></param>
        public override bool ConditionMet(NetworkConnection connection)
        {
            return connection.Scenes.Contains(NetworkObject.gameObject.scene);
        }

        /// <summary>
        /// True if the condition requires regular updates.
        /// </summary>
        /// <returns></returns>
        public override bool Timed()
        {
            return _timed;
        }


        /// <summary>
        /// Clones referenced ObserverCondition. This must be populated with your conditions settings.
        /// </summary>
        /// <returns></returns>
        public override ObserverCondition Clone()
        {
            SceneCondition copy = ScriptableObject.CreateInstance<SceneCondition>();
            copy.ConditionConstructor(_synchronizeScene, _timed);
            return copy;
        }

    }
}
