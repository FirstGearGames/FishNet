using UnityEngine;
using System.Collections.Generic;
using System;
using FishNet.Object.Helping;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace FishNet.Object
{
    public sealed partial class NetworkObject : MonoBehaviour
    {
        #region Serialized.

        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        private short _prefabId = -1;
        /// <summary>
        /// Id to use when spawning this object over the network as a prefab.
        /// </summary>
        public short PrefabId => _prefabId;
        /// <summary>
        /// Sets PrefabId.
        /// </summary>
        /// <param name="value"></param>
        internal void SetPrefabId(short value)
        {
            _prefabId = value;
        }
#pragma warning disable 414 //Disabled because Unity thinks tihs is unused when building.
        /// <summary>
        /// Hash to the scene which this object resides.
        /// </summary>
        [SerializeField, HideInInspector]
        private uint _scenePathHash;
#pragma warning restore 414
        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        private ulong _sceneId;
        /// <summary>
        /// Id for this scene object.
        /// </summary>
        internal ulong SceneId
        {
            get => _sceneId;
            private set => _sceneId = value;
        }
        #endregion

#if UNITY_EDITOR
        /// <summary>
        /// This is used to store NetworkObjects in the scene during edit time.
        /// SceneIds are compared against this collection to ensure there are no duplicated.
        /// </summary>
        [SerializeField, HideInInspector]
        private List<NetworkObject> _sceneNetworkObjects = new List<NetworkObject>();
#endif

        /// <summary>
        /// Removes SceneObject state.
        /// This may only be called at runtime.
        /// </summary>
        internal void ClearRuntimeSceneObject()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError($"ClearRuntimeSceneObject may only be called at runtime.");
                return;
            }

            SceneId = 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Tries to generate a SceneId.
        /// </summary>
        internal void TryCreateSceneID()
        {

            if (Application.isPlaying)
                return;
            if (gameObject == null)
                return;

            ulong startId = SceneId;
            uint startPath = _scenePathHash;

            ulong sceneId = 0;
            uint scenePathHash = 0;
            //If prefab or part of a prefab, not a scene object.            
            if (PrefabUtility.IsPartOfPrefabAsset(this) || IsEditingInPrefabMode() ||
             //Not in a scene, another prefab check.
             !gameObject.scene.IsValid() ||
             //Stored on disk, so is a prefab. Somehow prefabutility missed it.
             EditorUtility.IsPersistent(this))
            {
                //These are all failing conditions, don't do additional checks.
            }
            else
            {
                System.Random rnd = new System.Random();
                scenePathHash = gameObject.scene.path.ToLower().GetStableHash32();
                sceneId = SceneId;
                //Not a valid sceneId or is a duplicate. 
                if (scenePathHash != _scenePathHash || SceneId == 0 || IsDuplicateSceneId(SceneId))
                {
                    /* If a scene has not been opened since an id has been
                     * generated then it will not be serialized in editor. The id
                     * would be correct in build but not if running in editor. 
                     * Should conditions be true where scene is building without
                     * being opened then cancel build and request user to open and save
                     * scene. */
                    if (BuildPipeline.isBuildingPlayer)
                        throw new InvalidOperationException($"Networked GameObject {gameObject.name} in scene {gameObject.scene.path} is missing a SceneId. Open the scene, select the Fish-Networking menu, and choose Rebuild SceneIds. If the problem persist ensures {gameObject.name} does not have any missing script references on it's prefab or in the scene. Also ensure that you have any prefab changes for the object applied.");

                    ulong shiftedHash = (ulong)scenePathHash << 32;
                    ulong randomId = 0;
                    while (randomId == 0 || IsDuplicateSceneId(randomId))
                    {
                        uint next = (uint)(rnd.Next(int.MinValue, int.MaxValue) + int.MaxValue);
                        /* Since the collection is lost when a scene loads the it's possible to
                        * have a sceneid from another scene. Because of this the scene path is
                        * inserted into the sceneid. */
                        randomId = (next & 0xFFFFFFFF) | shiftedHash;
                    }

                    sceneId = randomId;
                }

            }

            bool idChanged = (sceneId != startId);
            bool pathChanged = (startPath != scenePathHash);
            //If either changed then dirty and set.
            if (idChanged || pathChanged)
            {
                //Set dirty so changes will be saved.
                EditorUtility.SetDirty(this);
                /* Add to sceneIds collection. This must be done
                 * even if a new sceneId was not generated because
                 * the collection information is lost when the
                 * scene is existed. Essentially, it gets repopulated
                 * when the scene is re-opened. */
                SceneId = sceneId;
                _scenePathHash = scenePathHash;
            }
        }

        private bool IsEditingInPrefabMode()
        {
            if (EditorUtility.IsPersistent(this))
            {
                // if the game object is stored on disk, it is a prefab of some kind, despite not returning true for IsPartOfPrefabAsset =/
                return true;
            }
            else
            {
                // If the GameObject is not persistent let's determine which stage we are in first because getting Prefab info depends on it
                StageHandle mainStage = StageUtility.GetMainStageHandle();
                StageHandle currentStage = StageUtility.GetStageHandle(gameObject);
                if (currentStage != mainStage)
                {
                    var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
                    if (prefabStage != null)
                    {
                        return true;
                    }
                }
            }
            return false;

        }

        /// <summary>
        /// Returns if the Id used is a sceneId already belonging to another object.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool IsDuplicateSceneId(ulong id)
        {
            //Find all nobs in scene.
            _sceneNetworkObjects = GameObject.FindObjectsOfType<NetworkObject>().ToList();
            foreach (NetworkObject nob in _sceneNetworkObjects)
            {
                if (nob != null && nob != this && nob.SceneId == id)
                    return true;
            }
            //If here all checks pass.
            return false;
        }

        partial void PartialOnValidate()
        {
            TryCreateSceneID();
        }
        partial void PartialReset()
        {
            TryCreateSceneID();
        }
#endif
    }

}

