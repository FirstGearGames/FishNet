using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;
using UnitySceneManagement = UnityEngine.SceneManagement;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {

        /* //todo: move all serializers and deserializers which require
         * a reference and are for internal use to their respected classes
         * as shown below. 
         * 
         * This offers organization by moving the serializers to the type
         * they are for.
         * 
         * This reduces chance of error by having to remember what data must
         * be discarded if the reference could not be found.
         * EG: ParseSceneChange reads the networkobject, and then reads a string
         * for the sceneName. If the networkObject is not found then the string
         * must be read and discarded. By putting the deserializer inside the type
         * it's a lot more clear what the job of the serializer is and what information
         * needs to be passed to the found instance of nob. */

        #region ChangeScene.
        /// <summary>
        /// Writes and sends a scene change to observers.
        /// </summary>
        private void WriteSceneChange()
        {
            PooledWriter writer = WriterPool.Retrieve();
            writer.WritePacketId(PacketId.SceneChange);
            writer.WriteNetworkObject(this);
            writer.WriteString(gameObject.scene.name);
            NetworkManager.TransportManager.SendToClients((byte)Channel.Reliable, writer.GetArraySegment(), Observers);
            WriterPool.Store(writer);
        }
        /// <summary>
        /// Reads and applies a scene change.
        /// </summary>
        internal static void ParseChangeScene(Reader reader)
        {
            NetworkObject nob = reader.ReadNetworkObject();
            string sceneName = reader.ReadString();

            if (nob != null)
                nob.ParseChangeScene(sceneName);
        }
        /// <summary>
        /// Reads and applies a scene change.
        /// </summary>
        private void ParseChangeScene(string sceneName)
        {
            /* If server is started there is no need to 
             * go beyond this as the object would have already moved
             * and invoked. */
            if (IsServerStarted)
                return;

            UnitySceneManagement.Scene s = UnitySceneManagement.SceneManager.GetSceneByName(sceneName);
            ChangeScene_Internal(s, asServer: false, move: true, sendToClients: false);
        }
        #endregion

        ///// <summary>
        ///// Reads and applies a scene change.
        ///// </summary>
        //internal static void ParseSetParent(Reader reader)
        //{
        //    NetworkObject nob = reader.ReadNetworkObject();
        //    string sceneName = reader.ReadString();

        //    if (nob != null)
        //        nob.ParseSceneChange(sceneName);
        //}

    }

}

