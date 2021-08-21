using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened.Data;
using FishNet.Managing.Scened.Eventing;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FirstGearGames.FlexSceneManager.Demos
{

    /// <summary>
    /// This will load every client which enters into their own stacked scene.
    /// </summary>
    public class UniqueSceneStackingExample : MonoBehaviour
    {
        /// <summary>
        /// Single scene to load. Leave empty to not load a single scene.
        /// </summary>
        [Tooltip("Single scene to load. Leave empty to not load a single scene.")]
        [SerializeField]
        private string _singleScene = string.Empty;
        /// <summary>
        /// True to move the triggering identity.
        /// </summary>
        [Tooltip("True to move the triggering identity.")]
        [SerializeField]
        private bool _moveIdentity = true;
        /// <summary>
        /// Additive scenes to load. Leave empty to not load additive scenes.
        /// </summary>
        [Tooltip("Additive scenes to load. Leave empty to not load additive scenes.")]
        [SerializeField]
        private string[] _additiveScenes = null;

        /// <summary>
        /// Scenes loaded for connections.
        /// </summary>
        private Dictionary<NetworkConnection, HashSet<Scene>> _loadedScenes = new Dictionary<NetworkConnection, HashSet<Scene>>();


        private void Start()
        {
            InstanceFinder.SceneManager.OnLoadSceneEnd += SceneManager_OnLoadSceneEnd;
            InstanceFinder.SceneManager.OnUnloadSceneEnd += SceneManager_OnUnloadSceneEnd;
        }

        private void FixedUpdate()
        {
            List<NetworkConnection> connectionsWithNullOrNoScenes = new List<NetworkConnection>();
            /* Simulate physics on each loaded scene. When using local physics scenes you
             * must do this otherwise physics will not tick. Be mindful of PhysicsScene casts as well:
             * https://docs.unity3d.com/2019.1/Documentation/ScriptReference/PhysicsScene.html */
            foreach (KeyValuePair<NetworkConnection, HashSet<Scene>> item in _loadedScenes)
            {
                //No scenes for connection.
                if (item.Value.Count == 0)
                {
                    connectionsWithNullOrNoScenes.Add(item.Key);
                    continue;
                }

                foreach (Scene s in item.Value)
                {
                    //If scene exist then simulate physics.
                    if (!string.IsNullOrEmpty(s.name))
                        s.GetPhysicsScene().Simulate(Time.deltaTime);
                    //Scene doesn't exist, queue it to be cleaned from connection.
                    else
                        connectionsWithNullOrNoScenes.Add(item.Key);
                }
            }

            for (int i = 0; i < connectionsWithNullOrNoScenes.Count; i++)
                CleanEmptyScenesFromLoaded(connectionsWithNullOrNoScenes[i]);
        }

        /// <summary>
        /// Received when scene loading ends.
        /// </summary>
        /// <param name="obj"></param>
        private void SceneManager_OnLoadSceneEnd(LoadSceneEndEventArgs args)
        {
            //Only server will manage loaded scenes.
            if (!InstanceFinder.NetworkManager.IsServer)
                return;
            //Not scoped for connections, or no connections specified.
            if (args.RawData.ScopeType != SceneScopeTypes.Connections || (args.RawData.Connections == null || args.RawData.Connections.Length == 0))
                return;

            foreach (NetworkConnection nc in args.RawData.Connections)
            {
                if (nc == null)
                    continue;

                HashSet<Scene> scenes;
                //If entry doesn't exist yet.
                if (!_loadedScenes.TryGetValue(nc, out scenes))
                {
                    scenes = new HashSet<Scene>();
                    _loadedScenes.Add(nc, scenes);
                }
                //Add loaded scenes.
                for (int i = 0; i < args.LoadedScenes.Length; i++)
                    scenes.Add(args.LoadedScenes[i]);
            }
        }

        /// <summary>
        /// Received when a scene unloading ends.
        /// </summary>
        /// <param name="args"></param>
        private void SceneManager_OnUnloadSceneEnd(UnloadSceneEndEventArgs args)
        {
            //Only server will manage loaded scenes.
            if (!InstanceFinder.NetworkManager.IsServer)
                return;
            //No need to process for networked scenes since this is for stacking scenes example, which will always be connection scenes.
            if (args.RawData.ScopeType != SceneScopeTypes.Connections)
                return;

            if (args.RawData.Connections != null)
            {
                for (int i = 0; i < args.RawData.Connections.Length; i++)
                    CleanEmptyScenesFromLoaded(args.RawData.Connections[i]);
            }
        }


        /// <summary>
        /// Removes empty scenes from loaded scenes.
        /// </summary>
        private void CleanEmptyScenesFromLoaded(NetworkConnection conn)
        {
            if (conn == null)
            {
                /* Cannot remove null from a dictionary, instead
                 * try to find a connection that isn't ready and remove it. 
                 * Ideally if you are doing something similar to this you will
                 * remove the connection as the client disconnects so the NetworkConnection
                 * is not null, rather than from the FSM callback. */
                List<NetworkConnection> connsToRemove = new List<NetworkConnection>();
                foreach (NetworkConnection item in _loadedScenes.Keys)
                {
                    if (!item.Authenticated)
                        connsToRemove.Add(item);
                }
                for (int i = 0; i < connsToRemove.Count; i++)
                    _loadedScenes.Remove(connsToRemove[i]);

                return;
            }

            HashSet<Scene> scenes;
            if (_loadedScenes.TryGetValue(conn, out scenes))
            {
                List<Scene> removeEntries = new List<Scene>();
                foreach (Scene s in scenes)
                {
                    if (string.IsNullOrEmpty(s.name))
                        removeEntries.Add(s);
                }

                for (int i = 0; i < removeEntries.Count; i++)
                    scenes.Remove(removeEntries[i]);
            }

            //If no more scenes remove connection reference.
            if (scenes != null && scenes.Count == 0)
                _loadedScenes.Remove(conn);
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

        private void LoadScene(NetworkObject triggeringObject)
        {
            if (triggeringObject == null)
                return;

            SingleSceneData ssd = null;
            //If to load a single scene.
            if (_singleScene != string.Empty)
            {
                List<NetworkObject> movedIdents = new List<NetworkObject>();
                if (_moveIdentity)
                    movedIdents.Add(triggeringObject);

                ssd = new SingleSceneData(_singleScene, movedIdents.ToArray());
            }

            //Additive.
            AdditiveScenesData asd = null;
            if (_additiveScenes != null && _additiveScenes.Length > 0)
                asd = new AdditiveScenesData(_additiveScenes);

            /* Stacking requires to be loaded for connection only. */
            /* Create a load options which allows to load scenes that are already loaded.
            * This is what enables stacking. You must also specify the type of physics
            * you wish to use when utilizing scene stacking. */
            LoadOptions loadOptions = new LoadOptions
            {
                LoadOnlyUnloaded = false,
                LocalPhysics = LocalPhysicsMode.Physics3D
            };
            InstanceFinder.SceneManager.LoadConnectionScenes(triggeringObject.Owner, ssd, asd, loadOptions);
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

            /* The server will now
             * unload all scenes for this connection. */
            List<SceneReferenceData> srds = new List<SceneReferenceData>();
            foreach (Scene item in loadedScenes)
                srds.Add(new SceneReferenceData(item));

            AdditiveScenesData asd = new AdditiveScenesData(srds.ToArray());
            InstanceFinder.SceneManager.UnloadConnectionScenes(triggeringIdentity.Owner, asd);
        }

    }


}