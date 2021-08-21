using UnityEngine;
using System.Collections.Generic;
using System;
using FishNet.Object.Helping;
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
        private uint _scenePathHash = 0;
#pragma warning restore 414
        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        private ulong _sceneId = 0;
        /// <summary>
        /// Id for this scene object.
        /// </summary>
        public ulong SceneId
        {
            get => _sceneId;
            private set => _sceneId = value;
        }
        #endregion

        #region Private.
        /// <summary>
        /// Contains NetworkObjects with their sceneIds are they are in their objects scene.
        /// </summary>
        [SerializeField, HideInInspector]
        private static readonly Dictionary<ulong, NetworkObject> _sceneIds = new Dictionary<ulong, NetworkObject>();
        #endregion

#if UNITY_EDITOR
        /// <summary>
        /// Tries to generate a SceneId.
        /// </summary>
        private void TryCreateSceneID()
        {
            //If prefab or part of a prefab, not a scene object.
            if (PrefabUtility.IsPartOfPrefabAsset(this) || IsEditingInPrefabMode())
            {
                /* //muchlater This likely has to be done on prefab too if user
                 * applies changes to prefab after editing in scene. */
                SceneId = 0;
                return;
            }
            //Not in a scene, another prefab check.
            if (!gameObject.scene.IsValid())
            {
                SceneId = 0;
                return;
            }
            //Stored on disk, so is a prefab. Somehow prefabutility missed it.
            if (EditorUtility.IsPersistent(this))
            {
                SceneId = 0;
                return;
            }

            //Do not execute if playing.
            if (Application.isPlaying)
                return;
            if (gameObject == null)
                return;

            System.Random rnd = new System.Random();

            uint scenePathHash = gameObject.scene.path.ToLower().GetStableHash32();
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
                    throw new InvalidOperationException($"Scene {gameObject.scene.path} needs to be opened and resaved before building, because the scene object {gameObject.name} has no valid sceneId yet.");

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

                SceneId = randomId;
            }

            //Set dirty so changes will be saved.
            EditorUtility.SetDirty(this);
            /* Add to sceneIds collection. This must be done
             * even if a new sceneId was not generated because
             * the collection information is lost when the
             * scene is existed. Essentially, it gets repopulated
             * when the scene is re-opened. */
            _sceneIds[SceneId] = this;
            _scenePathHash = scenePathHash;
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
                var mainStage = StageUtility.GetMainStageHandle();
                var currentStage = StageUtility.GetStageHandle(gameObject);
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
            bool inSceneIds = _sceneIds.TryGetValue(id, out NetworkObject nob);
            if (inSceneIds && nob != null && nob != this)
                return true;
            else
                return false;
        }

        protected virtual void OnValidate()
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

