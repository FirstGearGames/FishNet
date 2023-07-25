using FishNet.Managing.Scened;
using FishNet.Object;
using GameKit.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Demo.AdditiveScenes
{

    public class ServerScenePrewarmer : NetworkBehaviour
    {
        [SerializeField, Scene]
        private string[] _scenes = new string[0];

        public override void OnStartServer()
        {
            /* Load all the needed scenes at once.
             * This is not really required in most situations
             * but the server uses waypoints from each scene
             * to move the players. The waypoints won't exist unless
             * all scenes do. This can be seen in the Player
             * script. */

            /* Load scenes using the FishNet SceneManager, with automaticallyUnload
             * as false. This will keep the scenes from unloading when a client
             * disconnects or leaves the scene. */
            foreach (string item in _scenes)
            {
                SceneLookupData lookupData = new SceneLookupData(item);
                SceneLoadData sld = new SceneLoadData(lookupData)
                {
                    Options = new LoadOptions
                    {
                        AutomaticallyUnload = false
                    },
                };

                base.SceneManager.LoadConnectionScenes(sld);
            }
        }
    }

}