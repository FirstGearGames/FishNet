using FishNet.Managing.Logging;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Example.Scened
{

    /// <summary>
    /// Unloads specified scenes when entering or exiting this trigger.
    /// </summary>
    public class SceneUnloaderExample : MonoBehaviour
    {
        /// <summary>
        /// Scenes to unload.
        /// </summary>
        [Tooltip("Scenes to unload.")]
        [SerializeField]
        private string[] _scenes = new string[0];
        /// <summary>
        /// True to only unload for the connectioning causing the trigger.
        /// </summary>
        [Tooltip("True to only unload for the connectioning causing the trigger.")]
        [SerializeField]
        private bool _connectionOnly;
        /// <summary>
        /// True to unload unused scenes.
        /// </summary>
        [Tooltip("True to unload unused scenes.")]
        [SerializeField]
        private bool _unloadUnused = true;
        /// <summary>
        /// True to fire when entering the trigger. False to fire when exiting the trigger.
        /// </summary>
        [Tooltip("True to fire when entering the trigger. False to fire when exiting the trigger.")]
        [SerializeField]
        private bool _onTriggerEnter = true;


        [Server(Logging = LoggingType.Off)]
        private void OnTriggerEnter(Collider other)
        {
            if (!_onTriggerEnter)
                return;

            UnloadScenes(other.gameObject.GetComponent<NetworkObject>());
        }

        [Server(Logging = LoggingType.Off)]
        private void OnTriggerExit(Collider other)
        {
            if (_onTriggerEnter)
                return;

            UnloadScenes(other.gameObject.GetComponent<NetworkObject>());
        }

        /// <summary>
        /// Unload scenes.
        /// </summary>
        /// <param name="triggeringIdentity"></param>
        private void UnloadScenes(NetworkObject triggeringIdentity)
        {
            if (!InstanceFinder.NetworkManager.IsServer)
                return;

            //NetworkObject isn't necessarily needed but to ensure its the player only run if nob is found.
            if (triggeringIdentity == null)
                return;

            UnloadOptions unloadOptions = new UnloadOptions()
            {
                Mode = (_unloadUnused) ? UnloadOptions.ServerUnloadModes.UnloadUnused : UnloadOptions.ServerUnloadModes.KeepUnused
            };

            SceneUnloadData sud = new SceneUnloadData(_scenes);
            sud.Options = unloadOptions;

            //Unload only for the triggering connection.
            if (_connectionOnly)
                InstanceFinder.SceneManager.UnloadConnectionScenes(triggeringIdentity.Owner, sud);
            //Unload for all players.
            else
                InstanceFinder.SceneManager.UnloadGlobalScenes(sud);
        }


    }


}