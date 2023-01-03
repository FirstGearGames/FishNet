using FishNet.Managing.Logging;
using FishNet.Serializing.Helping;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened
{
    /// <summary>
    /// Extensions for SceneLookupData.
    /// </summary>
    internal static class SceneLookupDataExtensions
    {
        /// <summary>
        /// Returns References from SceneLookupData.
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public static string[] GetReferences(this SceneLookupData[] datas)
        {
            string[] references = new string[datas.Length];
            for (int i = 0; i < datas.Length; i++)
                references[i] = datas[i].Reference;

            return references;
        }
    }

    /// <summary>
    /// Data container for looking up, loading, or unloading a scene.
    /// </summary>
    public class SceneLookupData
    {
        /// <summary>
        /// Handle of the scene. If value is 0, then handle is not used.
        /// </summary>
        public int Handle;
        /// <summary>
        /// Reference to the scene.
        /// </summary>
        public string Reference = string.Empty;
        
        /// <summary>
        /// True if the Reference is a path to a scene.
        /// False if the Reference is the name of a scene.
        /// </summary>
        public bool IsPath = false;

        #region Const
        /// <summary>
        /// String to display when scene data is invalid.
        /// </summary>
        private const string INVALID_SCENE = "One or more scene information entries contain invalid data and have been skipped.";
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public SceneLookupData() { }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene">Scene to generate from.</param>
        public SceneLookupData(Scene scene)
        {
            Handle = scene.handle;
            Name = scene.name;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene">Scene to generate from.</param>
        /// <param name="isPath">True if scene is a scene path.</param>
        public SceneLookupData(string scene, bool isPath = false)
        {
            Reference = scene;
            IsPath = isPath;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle">Scene handle to generate from.</param>
        public SceneLookupData(int handle)
        {
            Handle = handle;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle">Scene handle to generate from.</param>
        /// <param name="scene">Name to generate from if handle is 0.</param>
        /// <param name="isPath">True if scene is a scene path.</param>
        public SceneLookupData(int handle, string scene, bool isPath = false)
        {
            Handle = handle;
            Reference = scene;
            IsPath = isPath;
        }

        #region Comparers.
        public static bool operator ==(SceneLookupData sldA, SceneLookupData sldB)
        {
            //One is null while the other is not.
            if ((sldA is null) != (sldB is null))
                return false;

            /*If here both are either null or have value. */
            if (!(sldA is null))
                return sldA.Equals(sldB);
            else if (!(sldB is null))
                return sldB.Equals(sldA);

            //Fall through indicates both are null.
            return true;
        }

        public static bool operator !=(SceneLookupData sldA, SceneLookupData sldB)
        {
            //One is null while the other is not.
            if ((sldA is null) != (sldB is null))
                return true;

            /*If here both are either null or have value. */
            if (!(sldA is null))
                return !sldA.Equals(sldB);
            else if (!(sldB is null))
                return !sldB.Equals(sldA);

            //Fall through indicates both are null.
            return true;
        }

        public bool Equals(SceneLookupData sld)
        {
            //Comparing instanced against null.
            if (sld is null)
                return false;

            //True if both handles are empty.
            bool bothHandlesEmpty = (
                (this.Handle == 0) &&
                (sld.Handle == 0)
                );

            //If both have handles and they match.
            if (!bothHandlesEmpty && sld.Handle == this.Handle)
                return true;
            //If neither have handles and name matches.
            else if (bothHandlesEmpty && sld.Name == this.Name)
                return true;

            //Fall through.
            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = 2053068273;
            hashCode = hashCode * -1521134295 + Handle.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Reference);
            hashCode = hashCode * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(IsPath);
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override string ToString()
        {
            return base.ToString();
        }
        #endregion

        #region CreateData.
        /// <summary>
        /// Returns a new SceneLookupData.
        /// </summary>
        /// <param name="scene">Scene to create from.</param>
        /// <returns></returns>
        public static SceneLookupData CreateData(Scene scene) => new SceneLookupData(scene);
        /// <summary>
        /// Returns a new SceneLookupData.
        /// </summary>
        /// <param name="scene">Scene to create from.</param>
        /// <param name="path">True if scene is a path to the scene.</param>
        /// <returns></returns>
        public static SceneLookupData CreateData(string scene, bool isPath = false) => new SceneLookupData(scene, isPath);
        /// <summary>
        /// Returns a new SceneLookupData.
        /// </summary>
        /// <param name="scene">Scene handle to create from.</param>
        /// <returns></returns>
        public static SceneLookupData CreateData(int handle) => new SceneLookupData(handle);
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scenes">Scenes to create from.</param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(List<Scene> scenes) => CreateData(scenes.ToArray());
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="names">Scene names to create from.</param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(List<string> names) => CreateData(names.ToArray());
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="handles">Scene handles to create from.</param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(List<int> handles) => CreateData(handles.ToArray());
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scenes">Scenes to create from.</param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(Scene[] scenes)
        {
            bool invalidFound = false;
            List<SceneLookupData> result = new List<SceneLookupData>();
            foreach (Scene item in scenes)
            {
                if (!item.IsValid())
                {
                    invalidFound = true;
                    continue;
                }

                result.Add(CreateData(item));
            }

            if (invalidFound)
                NetworkManager.StaticLogWarning(INVALID_SCENE);

            return result.ToArray();
        }
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scenes">Scenes to create from.</param>
        /// <param name="paths">True if scenes are paths to scenes. False if scenes are scene names.</param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(string[] scenes, bool paths = false)
        {
            bool invalidFound = false;
            List<SceneLookupData> result = new List<SceneLookupData>();
            foreach (string item in scenes)
            {
                if (string.IsNullOrEmpty(item))
                {
                    invalidFound = true;
                    continue;
                }
                
                if (paths)
                    result.Add(CreateData(item, true));
                else
                    result.Add(CreateData(System.IO.Path.GetFileNameWithoutExtension(item)));
            }

            if (invalidFound)
                NetworkManager.StaticLogWarning(INVALID_SCENE);

            return result.ToArray();
        }
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="handles">Scene handles to create from.</param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(int[] handles)
        {
            bool invalidFound = false;
            List<SceneLookupData> result = new List<SceneLookupData>();
            foreach (int item in handles)
            {
                if (item == 0)
                {
                    invalidFound = true;
                    continue;
                }

                result.Add(CreateData(item));
            }

            if (invalidFound)
                NetworkManager.StaticLogWarning(INVALID_SCENE);

            return result.ToArray();
        }
        #endregion

        /// <summary>
        /// Returns the first scene found using Handle or Reference, preferring Handle.
        /// </summary>
        /// <returns></returns>
        /// <param name="foundByHandle">True if scene was found by handle. Handle is always checked first.</param>
        public Scene GetScene(out bool foundByHandle)
        {
            foundByHandle = false;

            if (Handle == 0 && string.IsNullOrEmpty(Reference))
            {
                NetworkManager.StaticLogWarning("Scene handle and reference is unset; scene cannot be returned.");
                return default;
            }

            Scene result = default;

            //Lookup my handle.
            if (Handle != 0)
            {
                result = SceneManager.GetScene(Handle);
                if (result.handle != 0)
                    foundByHandle = true;
            }

            //If couldnt find handle try by string.
            if (!foundByHandle)
                result = SceneManager.GetScene(Reference);

            return result;
        }

    }
}
