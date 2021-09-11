using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing
{

    [CreateAssetMenu(menuName = "FishNet/Observers/Scene Condition", fileName = "New Scene Condition")]
    public class SceneCondition : ObserverCondition
    {
        #region Serialized.
        /// <summary>
        /// True to synchronize which scene the object was spawned in to clients. When true this object will be moved to the clients equivelant of the scene it was spawned in on the server. This setting does not continously move this object to the same scene.
        /// </summary>
        [Tooltip("True to synchronize which scene the object was spawned in to clients. When true this object will be moved to the clients equivelant of the scene it was spawned in on the server. This setting does not continously move this object to the same scene.")]
        [SerializeField]
        private bool _synchronizeScene = false;
        #endregion

        public void ConditionConstructor(bool synchronizeScene)
        {
            _synchronizeScene = synchronizeScene;
        }

        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection"></param>
        public override bool ConditionMet(NetworkConnection connection)
        {

            
            /* First try checking if the passed in connection has the same
             * scene this object is in, in their Scenes. */
            if (connection.Scenes.Contains(NetworkObject.gameObject.scene))
            { 
                return true;
            }
            else
            {
                /* If there is no owner then there is no reason to continue.
                 * The object is owned by the server therefor will only
                 * qualify for visibility with the scene it resides. */
                if (!base.NetworkObject.OwnerIsValid)
                    return false;

                /* If here the object has a owner. If connection shares any
                 * scenes with the owner then this object will be visibile. */
                foreach (Scene s in base.NetworkObject.Owner.Scenes)
                {
                    //Scenes match.
                    if (connection.Scenes.Contains(s))
                        return true;
                }

                //Fall through, no matches.
                return false;
            }
        }

        /// <summary>
        /// True if the condition requires regular updates.
        /// </summary>
        /// <returns></returns>
        public override bool Timed()
        {
            return false;
        }


        /// <summary>
        /// Clones referenced ObserverCondition. This must be populated with your conditions settings.
        /// </summary>
        /// <returns></returns>
        public override ObserverCondition Clone()
        {
            SceneCondition copy = ScriptableObject.CreateInstance<SceneCondition>();
            copy.ConditionConstructor(_synchronizeScene);
            return copy;
        }

    }
}
