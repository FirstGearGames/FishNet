
using FishNet.Serializing.Helping;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Data
{
    public static class SceneLookupDataExtensions
    {
        /// <summary>
        /// Returns Names from SceneLookupData.
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        public static string[] GetNames(this SceneLookupData[] datas)
        {
            string[] names = new string[datas.Length];
            for (int i = 0; i < datas.Length; i++)
                names[i] = datas[i].Name;

            return names;
        }
    }

    public class SceneLookupData
    {
        /// <summary>
        /// Handle of the scene. If value is 0, then handle is not used.
        /// </summary>
        public int Handle = 0;
        /// <summary>
        /// Name of the scene.
        /// </summary>
        public string Name = string.Empty;

        #region Const
        /// <summary>
        /// String to display when scene data is invalid.
        /// </summary>
        private const string INVALID_SCENE = "One or more scene information entries contain invalid data and have been skipped.";
        #endregion

        public SceneLookupData() { }
        public SceneLookupData(Scene scene)
        {
            Handle = scene.handle;
            Name = scene.name;
        }
        public SceneLookupData(string name)
        {
            Name = name;
        }

        public SceneLookupData(int handle)
        {
            Handle = handle;
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
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
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
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneLookupData CreateData(Scene scene) => new SceneLookupData(scene);
        /// <summary>
        /// Returns a new SceneLookupData.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneLookupData CreateData(string name) => new SceneLookupData(name);
        /// <summary>
        /// Returns a new SceneLookupData.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneLookupData CreateData(int handle) => new SceneLookupData(handle);
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(List<Scene> scene) => CreateData(scene.ToArray());
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(List<string> name) => CreateData(name.ToArray());
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(List<int> handle) => CreateData(handle.ToArray());
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scenes"></param>
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
                Debug.LogWarning(INVALID_SCENE);
            return result.ToArray();
        }
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static SceneLookupData[] CreateData(string[] names)
        {
            bool invalidFound = false;
            List<SceneLookupData> result = new List<SceneLookupData>();
            foreach (string item in names)
            {
                if (string.IsNullOrEmpty(item))
                {
                    invalidFound = true;
                    continue;
                }

                result.Add(CreateData(item));
            }

            if (invalidFound)
                Debug.LogWarning(INVALID_SCENE);
            return result.ToArray();
        }
        /// <summary>
        /// Returns a SceneLookupData collection.
        /// </summary>
        /// <param name="scene"></param>
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
                Debug.LogWarning(INVALID_SCENE);
            return result.ToArray();
        }
        #endregion

        /// <summary>
        /// Returns the first scene found using Handle or Name, preferring Handle.
        /// </summary>
        /// <returns></returns>
        public Scene GetScene(out bool foundByHandle)
        {
            foundByHandle = false;

            if (Handle == 0 && string.IsNullOrEmpty(Name))
            {
                Debug.LogWarning("Scene handle and name is unset; scene cannot be returned.");
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
                result = SceneManager.GetScene(Name);

            return result;
        }

    }
}
