using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace FishNet.Managing.Scened
{

    public abstract class SceneProcessorBase : MonoBehaviour
    {
        #region Protected.
        /// <summary>
        /// SceneManager for this processor.
        /// </summary>
        protected SceneManager SceneManager;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager">SceneManager which will be utilizing this class.</param>
        public virtual void Initialize(SceneManager manager)
        {
            SceneManager = manager;
        }
        /// <summary>
        /// Called when scene loading has begun.
        /// </summary>
        public virtual void LoadStart(LoadQueueData queueData) { }
        /// <summary>
        /// Called when scene loading has ended.
        /// </summary>
        public virtual void LoadEnd(LoadQueueData queueData) { }
        /// <summary>
        /// Called when scene unloading has begun within a load operation.
        /// </summary>
        public virtual void UnloadStart(LoadQueueData queueData) { }
        /// <summary>
        /// Called when scene unloading has ended within a load operation.
        /// </summary>
        public virtual void UnloadEnd(LoadQueueData queueData) { }
        /// <summary>
        /// Called when scene unloading has begun within an unload operation.
        /// </summary>
        public virtual void UnloadStart(UnloadQueueData queueData) { }
        /// <summary>
        /// Called when scene unloading has ended within an unload operation.
        /// </summary>
        public virtual void UnloadEnd(UnloadQueueData queueData) { }
        /// <summary>
        /// Begin loading a scene using an async method.
        /// </summary>
        /// <param name="sceneName">Scene name to load.</param>
        public abstract void BeginLoadAsync(string sceneName, LoadSceneParameters parameters);
        /// <summary>
        /// Begin unloading a scene using an async method.
        /// </summary>
        /// <param name="sceneName">Scene name to unload.</param>
        public abstract void BeginUnloadAsync(Scene scene);
        /// <summary>
        /// Returns if a scene load or unload percent is done.
        /// </summary>
        /// <returns></returns>
        public abstract bool IsPercentComplete();
        /// <summary>
        /// Returns the progress on the current scene load or unload.
        /// </summary>
        /// <returns></returns>
        public abstract float GetPercentComplete();
        /// <summary>
        /// Adds a scene to loaded scenes.
        /// </summary>
        /// <param name="scene">Scene loaded.</param>
        public virtual void AddLoadedScene(Scene scene) { }
        /// <summary>
        /// Returns scenes which were loaded during a load operation.
        /// </summary>
        public abstract List<Scene> GetLoadedScenes();
        /// <summary>
        /// Activates scenes which were loaded.
        /// </summary>
        public abstract void ActivateLoadedScenes();
        /// <summary>
        /// Returns if all asynchronized tasks are considered IsDone.
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerator AsyncsIsDone();

    }


}