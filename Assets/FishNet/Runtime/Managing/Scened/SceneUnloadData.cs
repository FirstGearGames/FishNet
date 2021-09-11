using FishNet.Object;
using FishNet.Serializing.Helping;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Data
{

    //[CodegenIncludeInternal]
    public class SceneUnloadData
    {
        /// <summary>
        /// SceneLookupData for each scene to load.
        /// </summary>
        public SceneLookupData[] SceneLookupDatas = new SceneLookupData[0];
        /// <summary>
        /// Parameters which may be set and will be included in load callbacks.
        /// </summary>
        public UnloadParams Params = new UnloadParams();
        /// <summary>
        /// Additional options to use for loaded scenes.
        /// </summary>
        public UnloadOptions Options = new UnloadOptions();

        public SceneUnloadData() { }
        public SceneUnloadData(Scene scene) : this(new Scene[] { scene }) { }
        public SceneUnloadData(string sceneName) : this(new string[] { sceneName }) { }
        public SceneUnloadData(int sceneHandle) : this(new int[] { sceneHandle }) { }
        public SceneUnloadData(List<Scene> scenes) : this(scenes.ToArray()) { }
        public SceneUnloadData(List<string> sceneNames) : this(sceneNames.ToArray()) { }
        public SceneUnloadData(List<int> sceneHandles) : this(sceneHandles.ToArray()) { }

        /// <summary>
        /// Returns if any data is invalid, such as null entries.
        /// </summary>
        /// <returns></returns>
        internal bool DataInvalid()
        {
            //Null values.
            if (Params == null || SceneLookupDatas == null ||
                Options == null)
                return true;

            return false;
        }

        public SceneUnloadData(Scene[] scenes)
        {
            SceneLookupDatas = SceneLookupData.CreateData(scenes);
        }

        public SceneUnloadData(string[] sceneNames)
        {
            SceneLookupDatas = SceneLookupData.CreateData(sceneNames);
        }
        public SceneUnloadData(int[] sceneHandles)
        {
            SceneLookupDatas = SceneLookupData.CreateData(sceneHandles);
        }

        public SceneUnloadData(SceneLookupData[] sceneLookupDatas)
        {
            SceneLookupDatas = sceneLookupDatas;
        }

    }


}