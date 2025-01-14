﻿using UnityEngine;
using System;
using GameKit.Utilities;
using System.Collections.Generic;
using FishNet.Utility.Extension;
#if UNITY_EDITOR
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        #region Serialized.

        /// <summary>
        /// Networked PrefabId assigned to this Prefab.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public ushort PrefabId { get; internal set; } = 0;
        /// <summary>
        /// Spawn collection to use assigned to this Prefab.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public ushort SpawnableCollectionId { get; internal set; } = 0;
#pragma warning disable 414 //Disabled because Unity thinks tihs is unused when building.
        /// <summary>
        /// Hash to the scene which this object resides.
        /// </summary>
        [SerializeField, HideInInspector]
        private uint _scenePathHash;
#pragma warning restore 414
        /// <summary>
        /// Network Id for this scene object.
        /// </summary>
        [field: SerializeField, HideInInspector]
        internal ulong SceneId { get; private set; }
        /// <summary>
        /// Hash for the path which this asset resides. This value is set during edit time.
        /// </summary> 
        [field: SerializeField, HideInInspector]
        public ulong AssetPathHash { get; private set; }
        /// <summary>
        /// Sets AssetPathhash value.
        /// </summary>
        /// <param name="value">Value to use.</param>
        public void SetAssetPathHash(ulong value) => AssetPathHash = value;
        #endregion

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
        internal void TryCreateSceneID(List<NetworkObject> sceneNobs)
        {
            if (Application.isPlaying)
                return;
            //Unity bug, sometimes this can be null depending on editor callback orders.
            if (gameObject == null)
                return;
            //Not a scene object.
            if (string.IsNullOrEmpty(gameObject.scene.name))
            {
                SceneId = 0;
                return;
            }
            // Porting in some checks from the updated version of fishnet to prevent spamming during runtime
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
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
                scenePathHash = gameObject.scene.path.ToLower().GetStableHashU32();
                sceneId = SceneId;
                //Not a valid sceneId or is a duplicate. 
                if (scenePathHash != _scenePathHash || SceneId == 0 || IsDuplicateSceneId(SceneId, sceneNobs))
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
                    while (randomId == 0 || IsDuplicateSceneId(randomId, sceneNobs))
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
        private bool IsDuplicateSceneId(ulong id, List<NetworkObject> sceneNobs)
        {
            if (sceneNobs == null)
            {
                //This is not a runtime operation so allocations are fine.
                sceneNobs = new List<NetworkObject>();
                Scenes.GetSceneNetworkObjects(gameObject.scene, false, false, ref sceneNobs);
            }

            foreach (NetworkObject n in sceneNobs)
            {
                if (n != null && n != this && n.SceneId == id)
                    return true;
            }

            //If here all checks pass.
            return false;
        }

        private void ReferenceIds_OnValidate()
        {
            TryCreateSceneID(null);
        }
        private void ReferenceIds_Reset()
        {
            TryCreateSceneID(null);
        }
#endif
    }

}

