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
        /// Single scene to load. Leave empty to not load a single scene.
        /// </summary>
        [Tooltip("Single scene to load. Leave empty to not load a single scene.")]
        [SerializeField]
        private string _singleScene = string.Empty;
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
        /// Additive scenes to load. Leave empty to not load additive scenes.
        /// </summary>
        [Tooltip("Additive scenes to load. Leave empty to not load additive scenes.")]
        [SerializeField]
        private string[] _additiveScenes = null;
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

            SingleSceneData ssd = null;
            //If to load a single scene.
            if (_singleScene != string.Empty)
            {
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

                ssd = new SingleSceneData(_singleScene, movedObjects.ToArray());
            }

            //Additive.
            AdditiveScenesData asd = null;
            if (_additiveScenes != null && _additiveScenes.Length > 0)
                asd = new AdditiveScenesData(_additiveScenes);

            //Load for connection only.
            if (_connectionOnly)
            {
                LoadOptions loadOptions = new LoadOptions
                {
                    AutomaticallyUnload = _automaticallyUnload,
                };
                InstanceFinder.SceneManager.LoadConnectionScenes(triggeringIdentity.Owner, ssd, asd, loadOptions);
            }
            //Load for all clients.
            else
            {
                InstanceFinder.SceneManager.LoadNetworkedScenes(ssd, asd);
            }


        }


    }




}