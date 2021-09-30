using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Object
{
    public class RpcAttribute : Attribute { }

    /// <summary>
    /// ServerRpc methods will send messages to the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ServerRpcAttribute : RpcAttribute
    {
        /// <summary>
        /// Whether or not the ServerRpc should only be run if executed by the owner of the object
        /// </summary>
        public bool RequireOwnership = true;
    }

    /// <summary>
    /// ObserversRpc methods will send messages to all observers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ObserversRpcAttribute : RpcAttribute
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
    }

    /// <summary>
    /// TargetRpc methods will send messages to a single client.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class TargetRpcAttribute : RpcAttribute { }

    /// <summary>
    /// Prevents a method from running if server is not active.
    /// <para>Can only be used inside a NetworkBehaviour</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ServerAttribute : Attribute
    {
        /// <summary>
        /// Type of using to use when IsServer check fails.
        /// </summary>
        public LoggingType Logging = LoggingType.Off;
    }

    /// <summary>
    /// Prevents this method from running if client is not active.
    /// <para>Can only be used inside a NetworkBehaviour</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ClientAttribute : Attribute
    {
        /// <summary>
        /// Type of using to use when IsClient check fails.
        /// </summary>
        public LoggingType Logging = LoggingType.Off;
        /// <summary>
        /// Whether or not the ServerRpc should only be run if executed by the owner of the object
        /// </summary>
        public bool RequireOwnership = false;
    }

    public enum LoggingType
    {
        Off,
        Warn,
        Error
    }

}


namespace FishNet.Object.Synchronizing
{ 

    /// <summary>
    /// SyncObjects are used to synchronize collections from the server to all clients automatically.
    /// <para>Value must be changed on server, not directly by clients.
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
    /// SyncVars are used to synchronize a variable from the server to all clients automatically.
    /// <para>Value must be changed on server, not directly by clients. Hook parameter allows you to define a client-side method to be invoked when the client gets an update from the server.</para>
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
        ///A function that should be called on the client when the value changes.
        ///</summary>
        public string OnChange;
    }

}