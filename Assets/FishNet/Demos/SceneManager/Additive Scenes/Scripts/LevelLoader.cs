using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Demo.AdditiveScenes
{
    public class LevelLoader : NetworkBehaviour
    {

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServer)
                return;

            Player player = GetPlayerOwnedObject(other);
            if (player == null)
                return;

            /* Create a lookup handle using this objects scene.
             * This is one of many ways FishNet knows what scene to load
             * for the clients. */
            SceneLookupData lookupData = new SceneLookupData(gameObject.scene);
            SceneLoadData sld = new SceneLoadData(lookupData)
            {
                /* Set automatically unload to false
                 * so the server does not unload this scene when
                 * there are no more connections in it. */
                Options = new LoadOptions()
                {
                    AutomaticallyUnload = false
                },
                /* Also move the client object to the new scene. 
                * This step is not required but may be desirable. */
                MovedNetworkObjects = new NetworkObject[] { player.NetworkObject },
                //Load scenes as additive.
                ReplaceScenes = ReplaceOption.None,
                //Set the preferred active scene so the client changes active scenes.
                PreferredActiveScene = lookupData,
            };

            base.SceneManager.LoadConnectionScenes(player.Owner, sld);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!base.IsServer)
                return;

            Player player = GetPlayerOwnedObject(other);
            if (player == null)
                return;

            /* Create a lookup handle using this objects scene.
             * This is one of many ways FishNet knows what scene to load
             * for the clients. */
            SceneLookupData lookupData = new SceneLookupData(gameObject.scene);
            /* Tell server to keep unused when unloading. This will keep
             * the scene even if there are no connections. 
             * This varies from AutomaticallyUnload slightly;
             * automatically unload will remove the scene on the server
             * if there are no more connections, such as if players
             * were to disconnect. But when manually telling a scene to
             * unload you must tell the server to keep it even if unused,
             * if that is your preference. */
            SceneUnloadData sud = new SceneUnloadData(lookupData)
            {
                Options = new UnloadOptions()
                {
                    Mode = UnloadOptions.ServerUnloadMode.KeepUnused
                }
            };

            base.SceneManager.UnloadConnectionScenes(player.Owner, sud);
        }

        /// <summary>
        /// Returns a Player script if the object is a player.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        private Player GetPlayerOwnedObject(Collider other)
        {
            /* When an object exits this trigger unload the level for the client. */
            Player player = other.GetComponent<Player>();
            //Not the player object.
            if (player == null)
                return null;
            //No owner??
            if (!player.Owner.IsActive)
                return null;

            return player;
        }
    }

}