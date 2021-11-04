using FishNet.Object.Helping;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing
{

    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    public class SyncVar<T> : SyncBase
    {
        #region Private.
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
        public SyncVar(WritePermission writePermission, ReadPermission readPermission, float sendRate, Channel channel, T value)
        {
            SetInitialValues(value);
            base.InitializeInstance(writePermission, readPermission, sendRate, channel, false);
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
        /// <returns>True if value was set.</returns>
        public bool SetValue(T nextValue, bool calledByUser)
        {
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
                return false;

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
                    UpdateValues(nextValue);
                }
                else
                {
                    /* //writepermission if using owner write permissions
                     * make sure caller is owner. */
                    if (Comparers.EqualityCompare<T>(this._value, nextValue))
                        return false;

                    _value = nextValue;
                }

                TryDirty();
            }
            else
            {

                /* Previously clients were not allowed to set values
                 * but this has been changed because clients may want
                 * to update values locally while occasionally
                 * letting the syncvar adjust their side. */

                UpdateValues(nextValue);
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

            return true;
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
            WriteDelta(obj0, false);
        }

        //Read isn't used by SyncVar<T>, it's done within the NB.
        //public override void Read(PooledReader reader) { }

        /// <summary>
        /// Gets current value.
        /// </summary>
        /// <param name="previousValue"></param>
        /// <returns></returns>
        public T GetValue(bool previousValue) => (previousValue) ? _previousClientValue : _value;

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        public override void Reset()
        {
            _value = _initialValue;
            base.Reset();
        }
    }
}


