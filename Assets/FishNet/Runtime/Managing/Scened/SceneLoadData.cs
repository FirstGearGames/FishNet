using FishNet.Object;
using FishNet.Serializing.Helping;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Data
{
    //[CodegenIncludeInternal]
    public class SceneLoadData
    {
        /// <summary>
        /// SceneLookupData for each scene to load.
        /// </summary>
        public SceneLookupData[] SceneLookupDatas = new SceneLookupData[0];
        /// <summary>
        /// NetworkObjects to move to the new scenes. Objects will be moved to the first scene.
        /// </summary>
        public NetworkObject[] MovedNetworkObjects = new NetworkObject[0];
        /// <summary>
        /// True to replace current scenes with new ones. When true the first scene will be loaded as the active scene and the rest additive.
        /// False to add scenes onto currently loaded scenes.
        /// </summary>
        public bool ReplaceScenes = false;
        /// <summary>
        /// Parameters which may be set and will be included in load callbacks.
        /// </summary>
        public LoadParams Params = new LoadParams();
        /// <summary>
        /// Additional options to use for loaded scenes.
        /// </summary>
        public LoadOptions Options = new LoadOptions();

        public SceneLoadData() { }
        public SceneLoadData(Scene scene) : this(new Scene[] { scene }, null) { }
        public SceneLoadData(string sceneName) : this(new string[] { sceneName }, null) { }
        public SceneLoadData(int sceneHandle) : this(new int[] { sceneHandle }, null) { }
        public SceneLoadData(List<Scene> scenes) : this(scenes.ToArray(), null) { }
        public SceneLoadData(List<string> sceneNames) : this(sceneNames.ToArray(), null) { }
        public SceneLoadData(List<int> sceneHandles) : this(sceneHandles.ToArray(), null) { }
        public SceneLoadData(Scene[] scenes) : this(scenes, null) { }
        public SceneLoadData(string[] sceneNames) : this(sceneNames, null) { }
        public SceneLoadData(int[] sceneHandles) : this(sceneHandles, null) { }
        public SceneLoadData(SceneLookupData[] sceneLookupDatas) : this(sceneLookupDatas, null) { }

        /// <summary>
        /// Returns if any data is invalid, such as null entries.
        /// </summary>
        /// <returns></returns>
        internal bool DataInvalid()
        {
            //Null values.
            if (Params == null || MovedNetworkObjects == null || SceneLookupDatas == null ||
                Options == null)
                return true;

            return false;
        }

        public SceneLoadData(Scene[] scenes, NetworkObject[] movedNetworkObjects)
        {
            SceneLookupDatas = SceneLookupData.CreateData(scenes);
            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }

        public SceneLoadData(string[] sceneNames, NetworkObject[] movedNetworkObjects)
        {
            SceneLookupDatas = SceneLookupData.CreateData(sceneNames);
            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }
        public SceneLoadData(int[] sceneHandles, NetworkObject[] movedNetworkObjects)
        {
            SceneLookupDatas = SceneLookupData.CreateData(sceneHandles);
            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }

        public SceneLoadData(SceneLookupData[] sceneLookupDatas, NetworkObject[] movedNetworkObjects)
        {
            SceneLookupDatas = sceneLookupDatas;

            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }

    }


}