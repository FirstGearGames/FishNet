using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened.Data;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

namespace FirstGearGames.FlexSceneManager.Demos
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
        private bool _moveAllObjects = false;
        /// <summary>
        /// True to replace current scenes with new scenes. First scene loaded will become active scene.
        /// </summary>
        [Tooltip("True to replace current scenes with new scenes. First scene loaded will become active scene.")]
        [SerializeField]
        private bool _replaceScenes = false;
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
        private bool _connectionOnly = false;
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


        [Server]
        private void OnTriggerEnter(Collider other)
        {
            if (!_onTriggerEnter)
                return;

            LoadScene(other.GetComponent<NetworkObject>());
        }

        [Server]
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
            sld.ReplaceScenes = _replaceScenes;
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