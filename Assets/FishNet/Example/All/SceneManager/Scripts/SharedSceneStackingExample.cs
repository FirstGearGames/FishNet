using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened.Data;
using FishNet.Managing.Scened.Eventing;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirstGearGames.FlexSceneManager.Demos
{

    /// <summary>
    /// This example shows how to load players into the same scene when using scene stacking.
    /// The first two players to enter the green sphere will be placed in the same scene.
    /// The third and following players will be placed in a new, stacked scene.
    /// </summary>
    public class SharedSceneStackingExample : MonoBehaviour
    {
        /// <summary>
        /// Additive scene to load.
        /// </summary>
        [Tooltip("Additive scene to load.")]
        [SerializeField]
        private string _additiveScene = string.Empty;

        /// <summary>
        /// Scenes loaded for connections.
        /// </summary>
        private Dictionary<NetworkConnection, HashSet<Scene>> _loadedScenes = new Dictionary<NetworkConnection, HashSet<Scene>>();


        private void Start()
        {
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd += FlexSceneManager_OnClientPresenceChangeEnd;
        }

        private void FlexSceneManager_OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            //Only store which scenes connections are in on the server.
            if (!InstanceFinder.NetworkManager.IsServer)
                return;
            //No connections to store scenes for.
            if (args.Connection == null)
                return;

            //Ignore changes that aren't for stacking. Only interested in stacking scenes for this demo.
            if (args.Scene.name != _additiveScene)
                return;
            /* Try to get the hashset for the connection.
             * The hashset contains which scenes the connection
             * is in. */
            HashSet<Scene> scenes;
            _loadedScenes.TryGetValue(args.Connection, out scenes);

            //If was added to the scene.
            if (args.Added)
            {
                //No scenes for connection yet, generate hashset and add to loaded scenes.
                if (scenes == null)
                {
                    scenes = new HashSet<Scene>();
                    _loadedScenes.Add(args.Connection, scenes);
                }
                //Add loaded scenes to hashset.
                scenes.Add(args.Scene);
            }
            //If was removed from the scene.
            else
            {
                if (scenes != null)
                {
                    //Remove loaded scenes.
                    scenes.Remove(args.Scene);
                    //Connection isn't in any more scenes, no need to keep it in the collection.
                    if (scenes.Count == 0)
                        _loadedScenes.Remove(args.Connection);
                }
            }

        }



        [Server]
        private void OnTriggerEnter(Collider other)
        {
            LoadScene(other.GetComponent<NetworkObject>());
        }

        [Server]
        private void OnTriggerExit(Collider other)
        {
            UnloadScene(other.GetComponent<NetworkObject>());
        }

        private void LoadScene(NetworkObject triggeringIdentity)
        {
            if (triggeringIdentity == null)
                return;
            if (string.IsNullOrEmpty(_additiveScene))
                return;

            //Additive.
            AdditiveScenesData asd;
            /* If a stacked scene is already loaded then grab the first scene handle. from loaded scenes.
             * You can of course specify any scene handle you wish to load clients into the same scene,
             * while using scene stacking. */
            if (_loadedScenes.Count > 0)
            {
                HashSet<Scene> scenes = _loadedScenes.First().Value;
                Scene firstScene = scenes.First();
                SceneReferenceData srd = new SceneReferenceData()
                {
                    Handle = firstScene.handle,
                    Name = firstScene.name
                };

                asd = new AdditiveScenesData(new SceneReferenceData[] { srd });
            }
            //A stacked scene doesn't exist yet, make a new one.
            else
            {
                /* When loading a stacked scene without using the handle
                 * a new scene will be generated rather than loading into
                 * the existing scene. */
                asd = new AdditiveScenesData(new string[] { _additiveScene });
            }

            /* Stacking requires to be loaded for connection only. */
            /* Create a load options which allows to load scenes that are already loaded.
             * This is what enables stacking. You must also specify the type of physics
             * you wish to use when utilizing scene stacking. */
            LoadOptions loadOptions = new LoadOptions
            {
                LoadOnlyUnloaded = false,
                LocalPhysics = LocalPhysicsMode.Physics3D
            };

            InstanceFinder.SceneManager.LoadConnectionScenes(triggeringIdentity.Owner, null, asd, loadOptions);
        }

        private void UnloadScene(NetworkObject triggeringIdentity)
        {
            if (triggeringIdentity == null)
                return;
            /* Dictionary doesn't have key for this connection which means it has
             * no knowledge of which scenes were loaded for it. Because of this,
             * scenes cannot be unloaded. */
            HashSet<Scene> loadedScenes;
            if (!_loadedScenes.TryGetValue(triggeringIdentity.Owner, out loadedScenes))
                return;

            /* If here, not using a return scene. The server will now
             * unload all scenes for this connection. Things will beak
             * on the clients end, but that's part of the demo. */
            List<SceneReferenceData> srds = new List<SceneReferenceData>();
            foreach (Scene item in loadedScenes)
                srds.Add(new SceneReferenceData(item));

            AdditiveScenesData asd = new AdditiveScenesData(srds.ToArray());

            /* Stacking requires to be unloaded for connection only. */
            InstanceFinder.SceneManager.UnloadConnectionScenes(triggeringIdentity.Owner, asd);
        }

    }


}