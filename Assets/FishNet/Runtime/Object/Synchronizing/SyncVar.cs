using FishNet.Documenting;
using FishNet.Object.Helping;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing
{
    [APIExclude]
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    public class SyncVar<T> : SyncBase
    {
        #region Types.
        /// <summary>
        /// Information needed to invoke a callback.
        /// </summary>
        private struct CachedOnChange
        {
            internal readonly T Previous;
            internal readonly T Next;

            public CachedOnChange(T previous, T next)
            {
                Previous = previous;
                Next = next;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Called when the SyncDictionary changes.
        /// </summary>
        public event Action<T, T, bool> OnChange;
        #endregion

        #region Private.
        /// <summary>
        /// Server OnChange event waiting for start callbacks.
        /// </summary>
        private CachedOnChange? _serverOnChange;
        /// <summary>
        /// Client OnChange event waiting for start callbacks.
        /// </summary>
        private CachedOnChange? _clientOnChange;
        /// <summary>
        /// Value before the network is initialized on the containing object.
        /// </summary>
        private T _initialValue;
        /// <summary>
        /// Previous value on the client.
        /// </summary>
        private T _previousClientValue;
        /// <summary>
        /// Current value on the server, or client.
        /// </summary>
        private T _value;
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyncVar(NetworkBehaviour nb, uint syncIndex, WritePermission writePermission, ReadPermission readPermission, float sendRate, Channel channel, T value)
        {
            SetInitialValues(value);
            base.InitializeInstance(nb, syncIndex, writePermission, readPermission, sendRate, channel, false);
        }

        /// <summary>
        /// Called when the SyncType has been registered, but not yet initialized over the network.
        /// </summary>
        protected override void Registered()
        {
            base.Registered();
            _initialValue = _value;
        }

        /// <summary>
        /// Sets initial values to next.
        /// </summary>
        /// <param name="next"></param>
        private void SetInitialValues(T next)
        {
            _initialValue = next;
            UpdateValues(next);
        }
        /// <summary>
        /// Sets current and previous values.
        /// </summary>
        /// <param name="next"></param>
        private void UpdateValues(T next)
        {
            _previousClientValue = next;
            _value = next;
        }
        /// <summary>
        /// Sets current value and marks the SyncVar dirty when able to. Returns if able to set value.
        /// </summary>
        /// <param name="calledByUser">True if SetValue was called in response to user code. False if from automated code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(T nextValue, bool calledByUser)
        {
            /* If not registered then that means Awake
             * has not completed on the owning class. This would be true
             * when setting values within awake on the owning class. Registered
             * is called at the end of awake, so it would be unset until awake completed.
             * 
             * Registered however will be true when setting from another script,
             * even if the owning class of this was just spawned. This is because
             * the unity cycle will fire awake on the object soon as it's spawned, 
             * completing awake, and the user would set the value after. */
            if (!base.IsRegistered)
                return;
            /* Don't include warning about setting values when not server.
             * SyncVars have an option to exclude owner when synchronizing
             * because the client may need to change values locally only. */

            bool isServer = base.NetworkBehaviour.IsServer;
            bool isClient = base.NetworkBehaviour.IsClient;
            /* If not client or server then set skipChecks
             * as true. When neither is true it's likely user is changing
             * value before object is initialized. This is allowed
             * but checks cannot be processed because they would otherwise
             * stop setting the value. */
            bool skipChecks = (!isClient && !isServer);

            //Object is deinitializing.
            if (!skipChecks && CodegenHelper.NetworkObject_Deinitializing(this.NetworkBehaviour))
                return;

            //If setting as server.
            if (calledByUser)
            {
                /* If skipping checks there's no need to dirty.
                 * Value is expected to be set on server and client since
                 * it's being set before the object is initialized. Should
                 * this not be the case then the user made a mistake. */
                //If skipping checks also update 
                if (skipChecks)
                {
                    T prev = _value;
                    UpdateValues(nextValue);
                    InvokeOnChange(prev, _value, calledByUser);
                }
                else
                {
                    /* //writepermission if using owner write permissions
                     * make sure caller is owner. */
                    if (Comparers.EqualityCompare<T>(this._value, nextValue))
                        return;

                    T prev = _value;
                    _value = nextValue;
                    InvokeOnChange(prev, _value, calledByUser);
                }

                TryDirty();
            }
            //Not called by user.
            else
            {

                /* Previously clients were not allowed to set values
                 * but this has been changed because clients may want
                 * to update values locally while occasionally
                 * letting the syncvar adjust their side. */

                T prev = _previousClientValue;
                UpdateValues(nextValue);
                InvokeOnChange(prev, _value, calledByUser);
                TryDirty();
            }


            /* Tries to dirty so update
             * is sent over network. This needs to be called
             * anytime the data changes because there is no way
             * to know if the user set the value on both server
             * and client or just one side. */
            void TryDirty()
            {
                //Cannot dirty when skipping checks.
                if (skipChecks)
                    return;

                if (calledByUser)
                    base.Dirty();
                //writepermission Once client write permissions are added this needs to be updated.
                //else if (!asServer && base.Settings.WritePermission == WritePermission.ServerOnly)
                //    base.Dirty();
            }
        }

        /// <summary>
        /// Invokes OnChanged callback.
        /// </summary>
        private void InvokeOnChange(T prev, T next, bool asServer)
        {
            if (OnChange != null)
            {
                if (asServer)
                {
                    if (base.NetworkBehaviour.OnStartServerCalled)
                        OnChange.Invoke(prev, next, asServer);
                    else
                        _serverOnChange = new CachedOnChange(prev, next);
                }
                else
                {
                    if (base.NetworkBehaviour.OnStartClientCalled)
                        OnChange.Invoke(prev, next, asServer);
                    else
                        _clientOnChange = new CachedOnChange(prev, next);
                }

            }
        }


        /// <summary>
        /// Called after OnStartXXXX has occurred.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal override void OnStartCallback(bool asServer)
        {
            base.OnStartCallback(asServer);

            if (OnChange != null)
            {
                CachedOnChange? change = (asServer) ? _serverOnChange : _clientOnChange;
                if (change != null)
                    InvokeOnChange(change.Value.Previous, change.Value.Next, asServer);
            }

            if (asServer)
                _serverOnChange = null;
            else
                _clientOnChange = null;
        }

        /// <summary>
        /// Writes current value.
        /// </summary>
        /// <param name="resetSyncTick">True to set the next time data may sync.</param>
        public override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            base.WriteDelta(writer, resetSyncTick);
            writer.Write<T>(_value);
        }

        /// <summary>
        /// Writes current value if not initialized value.
        /// </summary>m>
        public override void WriteFull(PooledWriter obj0)
        {
            if (Comparers.EqualityCompare<T>(_initialValue, _value))
                return;
            /* SyncVars only hold latest value, so just
             * write current delta. */
            WriteDelta(obj0, false);
        }

        //Read isn't used by SyncVar<T>, it's done within the NB.
        //public override void Read(PooledReader reader) { }

        /// <summary>
        /// Gets current value.
        /// </summary>
        /// <param name="calledByUser"></param>
        /// <returns></returns>
        public T GetValue(bool calledByUser) => (calledByUser) ? _value : _previousClientValue;

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _value = _initialValue;
            _previousClientValue = _initialValue;            
        }
    }
}


