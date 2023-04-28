﻿namespace FishNet.Managing.Scened
{
    /// <summary>
    /// Additional user-crafted data which can be included in scene load callbacks.
    /// </summary>
    public class LoadParams
    {
        /// <summary>
        /// Objects which are included in callbacks on the server when loading a scene. Can be useful for including unique information about the scene, such as match id. These are not sent to clients; use ClientParams for this.
        /// </summary>
        [System.NonSerialized]
        public object[] ServerParams = new object[0];
        /// <summary>
        /// Bytes which are sent to clients during scene loads. Can contain any information.
        /// </summary>
        public byte[] ClientParams = new byte[0];
    }

}