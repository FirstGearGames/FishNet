﻿//This file contains values serialized in editor or once at runtime.

using UnityEngine;
using System;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using FishNet.Managing;
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
        #region Public.
        /// <summary>
        /// Unity's internal id assigned to every asset file.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public string UnityAssetGuid { get; internal set; } = "";

        /// <summary>
        /// Networked PrefabId assigned to this Prefab.
        /// </summary>
        public PrefabId PrefabId { get { return _prefabId.PrefabId; } internal set { _prefabId.PrefabId = value; } }

        [SerializeField, HideInInspector]
        private PrefabId.SerializedPrefabId _prefabId;

        /// <summary>
        /// Spawn collection to use assigned to this Prefab.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public ushort SpawnableCollectionId { get; internal set; } = 0;

        /// <summary>
        /// Sets SceneId value. This is not synchronized automatically.
        /// </summary>
        /// <param name="sceneId"></param>
        public void SetSceneId(ulong sceneId) => SceneId = sceneId;

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

        #region Internal.
        /// <summary>
        /// Network Id for this scene object.
        /// </summary>
        [field: SerializeField, HideInInspector]
        internal ulong SceneId { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        internal TransformProperties SerializedTransformProperties = new();
        #endregion

        #region Private.
        /// <summary>
        /// Last time sceneIds were built automatically.
        /// </summary>
        [System.NonSerialized]
        private static double _lastSceneIdAutomaticRebuildTime;
        #endregion

        /// <summary>
        /// Removes SceneObject state.
        /// This may only be called at runtime.
        /// </summary>
        internal void ClearRuntimeSceneObject()
        {
            if (!Application.isPlaying)
            {
                NetworkManagerExtensions.LogError($"ClearRuntimeSceneObject may only be called at runtime.");
                return;
            }

            SceneId = NetworkObject.UNSET_SCENEID_VALUE;
        }

#if UNITY_EDITOR
        private void OnApplicationQuit()
        {
            _lastSceneIdAutomaticRebuildTime = 0;
        }

        /// <summary>
        /// Tries to generate a SceneIds for NetworkObjects in a scene.
        /// </summary>
        internal static void CreateSceneId(UnityEngine.SceneManagement.Scene scene, bool force, out int changed, out int found)
        {
            changed = 0;
            found = 0;
            if (Application.isPlaying)
                return;
            if (!scene.IsValid())
                return;
            if (!scene.isLoaded)
                return;

            HashSet<ulong> setIds = new();
            uint scenePathHash = scene.path.GetStableHashU32();
            List<NetworkObject> sceneNobs = new();

            Scenes.GetSceneNetworkObjects(scene, false, false, false, ref sceneNobs);
            found = sceneNobs.Count;
            System.Random rnd = new();

            //NetworkObjects which need their Ids rebuilt.
            List<NetworkObject> rebuildingNobs = new();

            foreach (NetworkObject item in sceneNobs)
            {
                bool canGenerate = (!item.IsSceneObject || !setIds.Add(item.SceneId));
                /* If an Id has not been generated yet or if it
                 * already exist then rebuild for this object. */
                 if (force || canGenerate)
                 {
                    item.SceneId = NetworkObject.UNSET_SCENEID_VALUE;
                    rebuildingNobs.Add(item);
                }
            }

            foreach (NetworkObject item in rebuildingNobs)
            {
                ulong nextSceneId = NetworkObject.UNSET_SCENEID_VALUE;
                while (nextSceneId == NetworkObject.UNSET_SCENEID_VALUE || setIds.Contains(nextSceneId))
                {
                    uint rndId = (uint)(rnd.Next(int.MinValue, int.MaxValue) + int.MaxValue);
                    nextSceneId = CombineHashes(scenePathHash, rndId);
                }

                ulong CombineHashes(uint a, uint b)
                {
                    return (b | a);
                }

                setIds.Add(nextSceneId);
                changed++;
                item.SceneId = nextSceneId;
                EditorUtility.SetDirty(item);
            }
        }

        /// <summary>
        /// Tries to generate a SceneId.
        /// </summary>
        private void CreateSceneId(bool force)
        {
            if (Application.isPlaying)
                return;
            //Unity bug, sometimes this can be null depending on editor callback orders.
            if (gameObject == null)
                return;
            //Not a scene object.
            if (string.IsNullOrEmpty(gameObject.scene.name))
            {
                SceneId = NetworkObject.UNSET_SCENEID_VALUE;
                return;
            }

            /* If building then only check if
             * scene networkobjects have their sceneIds
             * missing. */
            if (BuildPipeline.isBuildingPlayer)
            {
                //If prefab or part of a prefab, not a scene object.            
                if (PrefabUtility.IsPartOfPrefabAsset(this) || IsEditingInPrefabMode() ||
                    //Not in a scene, another prefab check.
                    !gameObject.scene.IsValid() ||
                    //Stored on disk, so is a prefab. Somehow prefabutility missed it.
                    EditorUtility.IsPersistent(this))
                    //If here this is a sceneObject, but sceneId is not set.
                    if (!IsSceneObject)
                        throw new InvalidOperationException($"Networked GameObject {gameObject.name} in scene {gameObject.scene.path} is missing a SceneId. Open the scene, select the Fish-Networking menu, and choose Rebuild SceneIds. If the problem persist ensures {gameObject.name} does not have any missing script references on it's prefab or in the scene. Also ensure that you have any prefab changes for the object applied.");
            }
            //If not building check to rebuild sceneIds this for object and the scene its in.
            else
            {
                double realtime = EditorApplication.timeSinceStartup;
                //Only do this once every Xms to prevent excessive rebiulds.
                if (realtime - _lastSceneIdAutomaticRebuildTime < 0.250d)
                    return;

                //Not in a scene, another prefab check.
                //Stored on disk, so is a prefab. Somehow prefabutility missed it.
                if (PrefabUtility.IsPartOfPrefabAsset(this) || IsEditingInPrefabMode() || !gameObject.scene.IsValid() || EditorUtility.IsPersistent(this))
                    return;

                _lastSceneIdAutomaticRebuildTime = realtime;

                CreateSceneId(gameObject.scene, force, out _, out _);
            }
        }

        internal bool SetUnityAssetGuidAndDirty(string guid)
        {
            if (Application.isPlaying)
                return false;
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }
            UnityAssetGuid = guid;
            EditorUtility.SetDirty(this);

            return true;
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

        private void ReferenceIds_OnValidate()
        {
            CreateSceneId(force: false);
        }

        private void ReferenceIds_Reset()
        {
            CreateSceneId(force: false);
        }
#endif
    }
}