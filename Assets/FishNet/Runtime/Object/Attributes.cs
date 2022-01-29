using FishNet.Managing.Logging;
using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Object
{

    /// <summary>
    /// ServerRpc methods will send messages to the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ServerRpcAttribute : Attribute
    {
        /// <summary>
        /// True to only allow the owning client to call this RPC.
        /// </summary>
        public bool RequireOwnership = true;
        /// <summary>
        /// True to also run the RPC logic locally.
        /// </summary>
        public bool RunLocally = false;
    }

    /// <summary>
    /// ObserversRpc methods will send messages to all observers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ObserversRpcAttribute : Attribute
    {
        /// <summary>
        /// True to also send data to the owner of object.
        /// </summary>
        public bool IncludeOwner = true;
        /// <summary>
        /// True to buffer the last value and send it to new players when the object is spawned for them.
        /// RPC will be sent on the same channel as the original RPC, and immediately before the OnSpawnServer override.
        /// </summary>
        public bool BufferLast = false;
        /// <summary>
        /// True to also run the RPC logic locally.
        /// </summary>
        public bool RunLocally = false;
    }

    /// <summary>
    /// TargetRpc methods will send messages to a single client.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class TargetRpcAttribute : Attribute 
    {
        /// <summary>
        /// True to also run the RPC logic locally.
        /// </summary>
        public bool RunLocally = false;
    }

    /// <summary>
    /// Prevents a method from running if server is not active.
    /// <para>Can only be used inside a NetworkBehaviour</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ServerAttribute : Attribute
    {
        /// <summary>
        /// Type of logging to use when the IsServer check fails.
        /// </summary>
        public LoggingType Logging = LoggingType.Warning;
    }

    /// <summary>
    /// Prevents this method from running if client is not active.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ClientAttribute : Attribute
    {
        /// <summary>
        /// Type of logging to use when the IsClient check fails.
        /// </summary>
        public LoggingType Logging = LoggingType.Warning;
        /// <summary>
        /// True to only allow a client to run the method if they are owner of the object.
        /// </summary>
        public bool RequireOwnership = false;
    }
}


namespace FishNet.Object.Synchronizing
{

    /// <summary>
    /// Synchronizes collections or objects from the server to clients. Can be used with custom SyncObjects.
    /// Value must be changed on server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class SyncObjectAttribute : PropertyAttribute
    {
        /// <summary>
        /// How often values may update over the network.
        /// </summary>
        public float SendRate = 0.1f;
        /// <summary>
        /// Clients which may receive value updates.
        /// </summary>
        public ReadPermission ReadPermissions = ReadPermission.Observers;
    }

    /// <summary>
    /// Synchronizes a variable from server to clients automatically.
    /// Value must be changed on server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class SyncVarAttribute : PropertyAttribute
    {
        /// <summary>
        /// How often values may update over the network.
        /// </summary>
        public float SendRate = 0.1f;
        /// <summary>
        /// Clients which may receive value updates.
        /// </summary>
        public ReadPermission ReadPermissions = ReadPermission.Observers;
        /// <summary>
        /// Channel to use. Unreliable SyncVars will use eventual consistency.
        /// </summary>
        public Channel Channel;
        ///<summary>
        /// Method which will be called on the server and clients when the value changes.
        ///</summary>
        public string OnChange;
    }

}