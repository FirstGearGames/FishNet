using FishNet.Connection;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Eventing
{
    public struct ClientPresenceChangeEventArgs
    {

        /// <summary>
        /// Scene on the server which the client's presence has changed.
        /// </summary>
        public Scene Scene;
        /// <summary>
        /// Connection to client.
        /// </summary>
        public NetworkConnection Connection;
        /// <summary>
        /// True if the client was added to the scene, false is removed.
        /// </summary>
        public bool Added;

        public ClientPresenceChangeEventArgs(Scene scene, NetworkConnection conn, bool added)
        {
            Scene = scene;
            Connection = conn;
            Added = added;
        }
    }


}