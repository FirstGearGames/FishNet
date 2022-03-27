using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Scened;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Example.Scened
{

    /// <summary>
    /// Loads a single scene, additive scenes, or both when a client
    /// enters or exits this trigger.
    /// </summary>
    public class SceneLoaderExample : MonoBehaviour
    {
        /// <summary>
        /// True to move the triggering object.
        /// </summary>
        [Tooltip("True to move the triggering object.")]
        [SerializeField]
        private bool _moveObject = true;
        /// <summary>
        /// True to move all connection objects (clients).
        /// </summary>
        [Tooltip("True to move all connection objects (clients).")]
        [SerializeField]
        private bool _moveAllObjects;
        /// <summary>
        /// True to replace current scenes with new scenes. First scene loaded will become active scene.
        /// </summary>
        [Tooltip("True to replace current scenes with new scenes. First scene loaded will become active scene.")]
        [SerializeField]
        private bool _replaceScenes;
        /// <summary>
        /// Scenes to load.
        /// </summary>
        [Tooltip("Scenes to load.")]
        [SerializeField]
        private string[] _scenes = new string[0];
        /// <summary>
        /// True to only unload for the connectioning causing the trigger.
        /// </summary>
        [Tooltip("True to only unload for the connectioning causing the trigger.")]
        [SerializeField]
        private bool _connectionOnly;
        /// <summary>
        /// True to automatically unload the loaded scenes when no more connections are using them.
        /// </summary>
        [Tooltip("True to automatically unload the loaded scenes when no more connections are using them.")]
        [SerializeField]
        private bool _automaticallyUnload = true;
        /// <summary>
        /// True to fire when entering the trigger. False to fire when exiting the trigger.
        /// </summary>
        [Tooltip("True to fire when entering the trigger. False to fire when exiting the trigger.")]
        [SerializeField]
        private bool _onTriggerEnter = true;

        /// <summary>
        /// Used to prevent excessive triggering when two clients are loaded and server is separate.
        /// Client may enter trigger intentionally then when moved to a new scene will re-enter trigger
        /// since original scene will still be loaded on server due to another client being in it.
        /// This scenario is extremely unlikely in production but keep it in mind.
        /// </summary>
        private Dictionary<NetworkConnection, float> _triggeredTimes = new Dictionary<NetworkConnection, float>();


        [Server(Logging = LoggingType.Off)]
        private void OnTriggerEnter(Collider other)
        {
            if (!_onTriggerEnter)
                return;

            LoadScene(other.GetComponent<NetworkObject>());
        }

        [Server(Logging = LoggingType.Off)]
        private void OnTriggerExit(Collider other)
        {
            if (_onTriggerEnter)
                return;

            LoadScene(other.GetComponent<NetworkObject>());
        }

        private void LoadScene(NetworkObject triggeringIdentity)
        {
            if (!InstanceFinder.NetworkManager.IsServer)
                return;

            //NetworkObject isn't necessarily needed but to ensure its the player only run if found.
            if (triggeringIdentity == null)
                return;

            /* Dont let trigger hit twice by same connection too frequently
             * See _triggeredTimes field for more info. */
            if (_triggeredTimes.TryGetValue(triggeringIdentity.Owner, out float time))
            {
                if (Time.time - time < 0.5f)
                    return;
            }
            _triggeredTimes[triggeringIdentity.Owner] = Time.time;

            //Which objects to move.
            List<NetworkObject> movedObjects = new List<NetworkObject>();
            if (_moveAllObjects)
            {
                foreach (NetworkConnection item in InstanceFinder.ServerManager.Clients.Values)
                {
                    foreach (NetworkObject nob in item.Objects)
                        movedObjects.Add(nob);
                }
            }
            else if (_moveObject)
            {
                movedObjects.Add(triggeringIdentity);
            }
            //Load options.
            LoadOptions loadOptions = new LoadOptions
            {
                AutomaticallyUnload = _automaticallyUnload,
            };

            //Make scene data.
            SceneLoadData sld = new SceneLoadData(_scenes);
            sld.ReplaceScenes = (_replaceScenes) ? ReplaceOption.All : ReplaceOption.None;
            sld.Options = loadOptions;
            sld.MovedNetworkObjects = movedObjects.ToArray();

            //Load for connection only.
            if (_connectionOnly)
                InstanceFinder.SceneManager.LoadConnectionScenes(triggeringIdentity.Owner, sld);
            //Load for all clients.
            else
                InstanceFinder.SceneManager.LoadGlobalScenes(sld);


        }


    }




}