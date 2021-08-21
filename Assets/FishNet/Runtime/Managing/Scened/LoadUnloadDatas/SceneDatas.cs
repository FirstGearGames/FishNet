using FishNet.Object;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Data
{


    public class SingleSceneData
    {
        /// <summary>
        /// SceneReferenceData for each scene to load.
        /// </summary>
        public SceneReferenceData SceneReferenceData;
        /// <summary>
        /// NetworkObjects to move to the new single scene.
        /// </summary>
        public NetworkObject[] MovedNetworkObjects;

        /// <summary>
        /// String to display when a scene name is null or empty.
        /// </summary>
        private const string NULL_EMPTY_SCENE_NAME = "SingleSceneData is being generated using a null or empty sceneName. If this was intentional, you may ignore this warning.";

        public SingleSceneData()
        {
            SceneReferenceData = new SceneReferenceData();
            MovedNetworkObjects = new NetworkObject[0];
        }

        public SingleSceneData(string sceneName) : this(sceneName, null) { }
        public SingleSceneData(string sceneName, NetworkObject[] movedNetworkObjects)
        {
            if (string.IsNullOrEmpty(sceneName))
                UnityEngine.Debug.LogWarning(NULL_EMPTY_SCENE_NAME);

            SceneReferenceData = new SceneReferenceData() { Name = sceneName };

            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }

        public SingleSceneData(SceneReferenceData sceneReferenceData) : this(sceneReferenceData, null) { }
        public SingleSceneData(SceneReferenceData sceneReferenceData, NetworkObject[] movedNetworkObjects)
        {
            SceneReferenceData = sceneReferenceData;

            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }
    }

    public class AdditiveScenesData
    {
        /// <summary>
        /// SceneReferenceData for each scene to load.
        /// </summary>
        public SceneReferenceData[] SceneReferenceDatas;
        /// <summary>
        /// NetworkObjects to move to the new single scene.
        /// </summary>
        public NetworkObject[] MovedNetworkObjects;

        /// <summary>
        /// String to display when scene names is null or of zero length.
        /// </summary>
        private const string NULL_SCENE_NAME_COLLECTION = "AdditiveScenesData is being generated using null or empty sceneNames. If this was intentional, you may ignore this warning.";
        /// <summary>
        /// String to display when a scene name is null or empty.
        /// </summary>
        private const string NULL_EMPTY_SCENE_NAME = "AdditiveSceneData is being generated using a null or empty sceneName. If this was intentional, you may ignore this warning.";

        public AdditiveScenesData()
        {
            SceneReferenceDatas = new SceneReferenceData[0];
            MovedNetworkObjects = new NetworkObject[0];
        }

        public AdditiveScenesData(string[] sceneNames) : this(sceneNames, null) { }
        public AdditiveScenesData(string[] sceneNames, NetworkObject[] movedNetworkObjects)
        {
            if (sceneNames == null || sceneNames.Length == 0)
                UnityEngine.Debug.LogWarning(NULL_SCENE_NAME_COLLECTION);

            SceneReferenceDatas = new SceneReferenceData[sceneNames.Length];
            for (int i = 0; i < sceneNames.Length; i++)
            {
                if (string.IsNullOrEmpty(sceneNames[i]))
                    UnityEngine.Debug.LogWarning(NULL_EMPTY_SCENE_NAME);

                SceneReferenceDatas[i] = new SceneReferenceData { Name = sceneNames[i] };
            }

            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }


        public AdditiveScenesData(SceneReferenceData[] sceneReferenceDatas) : this(sceneReferenceDatas, null) { }

        public AdditiveScenesData(SceneReferenceData[] sceneReferenceDatas, NetworkObject[] movedNetworkObjects)
        {
            SceneReferenceDatas = sceneReferenceDatas;

            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }


        public AdditiveScenesData(Scene[] scenes) : this(scenes, null) { }
        public AdditiveScenesData(Scene[] scenes, NetworkObject[] movedNetworkObjects)
        {
            if (scenes == null || scenes.Length == 0)
                UnityEngine.Debug.LogWarning(NULL_SCENE_NAME_COLLECTION);

            SceneReferenceDatas = new SceneReferenceData[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                if (string.IsNullOrEmpty(scenes[i].name))
                    UnityEngine.Debug.LogWarning(NULL_EMPTY_SCENE_NAME);

                SceneReferenceDatas[i] = new SceneReferenceData(scenes[i]);
            }

            if (movedNetworkObjects == null)
                movedNetworkObjects = new NetworkObject[0];
            MovedNetworkObjects = movedNetworkObjects;
        }
    }


}