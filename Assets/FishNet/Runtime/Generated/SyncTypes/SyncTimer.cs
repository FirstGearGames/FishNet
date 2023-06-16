using FishNet.Documenting;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FishNet.Object.Synchronizing
{

    /// <summary>
    /// A SyncObject to efficiently synchronize timers over the network.
    /// </summary>
    public class SyncTimer : SyncBase, ICustomSync
    {

        #region Type.
        /// <summary>
        /// Information about how the timer has changed.
        /// </summary>
        private struct ChangeData
        {
            public readonly SyncTimerOperation Operation;
            public readonly float Previous;
            public readonly float Next;

            public ChangeData(SyncTimerOperation operation, float previous, float next)
            {
                Operation = operation;
                Previous = previous;
                Next = next;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Delegate signature for when the timer operation occurs.
        /// </summary>
        /// <param name="op">Operation which was performed.</param>
        /// <param name="prev">Previous value of the timer. This will be -1f is the value is not available.</param>
        /// <param name="next">Value of the timer. This will be -1f is the value is not available.</param>
        /// <param name="asServer">True if occurring on server.</param>
        public delegate void SyncTypeChanged(SyncTimerOperation op, float prev, float next, bool asServer);
        /// <summary>
        /// Called when a timer operation occurs.
        /// </summary>
        public event SyncTypeChanged OnChange;
        /// <summary>
        /// Time remaining on the timer. When the timer is expired this value will be 0f.
        /// </summary>
        public float Remaining { get; private set; }
        /// <summary>
        /// How much time has passed since the timer started.
        /// </summary>
        public float Elapsed => (Duration - Remaining);
        /// <summary>
        /// Starting duration of the timer.
        /// </summary>
        public float Duration { get; private set; }
        /// <summary>
        /// True if the SyncTimer is currently paused. Calls to Update(float) will be ignored when paused.
        /// </summary>
        public bool Paused { get; private set; }
        #endregion

        #region Private.
        /// <summary>
        /// Changed data which will be sent next tick.
        /// </summary>
        private List<ChangeData> _changed = new List<ChangeData>();
        /// <summary>
        /// Server OnChange events waiting for start callbacks.
        /// </summary>
        private readonly List<ChangeData> _serverOnChanges = new List<ChangeData>();
        /// <summary>
        /// Client OnChange events waiting for start callbacks.
        /// </summary>
        private readonly List<ChangeData> _clientOnChanges = new List<ChangeData>();

        #endregion

        /// <summary>
        /// Starts a timer. If called when a timer is already active then StopTimer will automatically be sent.
        /// </summary>
        /// <param name="remaining">Time in which the timer should start with.</param>
        /// <param name="sendRemainingOnStop">True to include remaining time when automatically sending StopTimer.</param>
        public void StartTimer(float remaining, bool sendRemainingOnStop = true)
        {
            if (!base.CanNetworkSetValues(true))
                return;

            if (Remaining > 0f)
                StopTimer(sendRemainingOnStop);

            Paused = false;
            Remaining = remaining;
            Duration = remaining;
            AddOperation(SyncTimerOperation.Start, -1f, remaining);
        }

        /// <summary>
        /// Pauses the timer. Calling while already paused will be result in no action.
        /// </summary>
        /// <param name="sendRemaining">True to send Remaining with this operation.</param>
        public void PauseTimer(bool sendRemaining = false)
        {
            if (Remaining <= 0f)
                return;
            if (Paused)
                return;
            if (!base.CanNetworkSetValues(true))
                return;

            Paused = true;
            SyncTimerOperation op = (sendRemaining) ? SyncTimerOperation.PauseUpdated : SyncTimerOperation.Pause;
            AddOperation(op, Remaining, Remaining);
        }

        /// <summary>
        /// Unpauses the timer. Calling while already unpaused will be result in no action.
        /// </summary>
        public void UnpauseTimer()
        {
            if (Remaining <= 0f)
                return;
            if (!Paused)
                return;
            if (!base.CanNetworkSetValues(true))
                return;

            Paused = false;
            AddOperation(SyncTimerOperation.Unpause, Remaining, Remaining);
        }

        /// <summary>
        /// Stops and resets the timer. 
        /// </summary>
        public void StopTimer(bool sendRemaining = false)
        {
            if (Remaining <= 0f)
                return;
            if (!base.CanNetworkSetValues(true))
                return;

            bool asServer = true;
            float prev = Remaining;
            StopTimer_Internal(asServer);
            SyncTimerOperation op = (sendRemaining) ? SyncTimerOperation.StopUpdated : SyncTimerOperation.Stop;
            AddOperation(op, prev, 0f);
        }

        /// <summary>
        /// Adds an operation to synchronize.
        /// </summary>
        private void AddOperation(SyncTimerOperation operation, float prev, float next)
        {
            if (!base.IsRegistered)
                return;

            bool asServerInvoke = (!base.IsNetworkInitialized || base.NetworkBehaviour.IsServer);

            if (asServerInvoke)
            {
                if (base.Dirty())
                {
                    ChangeData change = new ChangeData(operation, prev, next);
                    _changed.Add(change);
                }
            }

            OnChange?.Invoke(operation, prev, next, asServerInvoke);
        }

        /// <summary>
        /// Writes all changed values.
        /// </summary>
        ///<param name="resetSyncTick">True to set the next time data may sync.</param>
        public override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            base.WriteDelta(writer, resetSyncTick);
            writer.WriteInt32(_changed.Count);

            for (int i = 0; i < _changed.Count; i++)
            {
                ChangeData change = _changed[i];
                writer.WriteByte((byte)change.Operation);

                if (change.Operation == SyncTimerOperation.Start)
                {
                    WriteStartTimer(writer, false);
                }
                //Pause and unpause updated need current value written.
                //Updated stop also writes current value.
                else if (change.Operation == SyncTimerOperation.PauseUpdated || change.Operation == SyncTimerOperation.StopUpdated)
                {
                    writer.WriteSingle(change.Next);
                }
            }

            _changed.Clear();
        }

        /// <summary>
        /// Writes all values.
        /// </summary>
        public override void WriteFull(PooledWriter writer)
        {
            //Only write full if a timer is running.
            if (Remaining <= 0f)
                return;

            base.WriteDelta(writer, false);
            //There will be 1 or 2 entries. If paused 2, if not 1.
            int entries = (Paused) ? 2 : 1;
            writer.WriteInt32(entries);
            //And the operations.
            WriteStartTimer(writer, true);
            if (Paused)
                writer.WriteByte((byte)SyncTimerOperation.Pause);
        }

        /// <summary>
        /// Writes a StartTimer operation.
        /// </summary>
        /// <param name="w"></param>
        /// <param name="includeOperationByte"></param>
        private void WriteStartTimer(Writer w, bool includeOperationByte)
        {
            if (includeOperationByte)
                w.WriteByte((byte)SyncTimerOperation.Start);
            w.WriteSingle(Remaining);
            w.WriteSingle(Duration);
        }

        /// <summary>
        /// Returns if values can be updated.
        /// </summary>
        private bool CanSetValues(bool asServer)
        {
            return (asServer || !base.NetworkManager.IsServer);
        }

        /// <summary>
        /// Reads and sets the current values for server or client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [APIExclude]
        public override void Read(PooledReader reader, bool asServer)
        {
            int changes = reader.ReadInt32();

            for (int i = 0; i < changes; i++)
            {
                SyncTimerOperation op = (SyncTimerOperation)reader.ReadByte();
                if (op == SyncTimerOperation.Start)
                {
                    float next = reader.ReadSingle();
                    float duration = reader.ReadSingle();
                    if (CanSetValues(asServer))
                    {
                        Paused = false;
                        Remaining = next;
                        Duration = duration;
                    }
                    InvokeOnChange(op, -1f, next, asServer);
                }
                else if (op == SyncTimerOperation.Pause || op == SyncTimerOperation.PauseUpdated
                    || op == SyncTimerOperation.Unpause)
                {
                    UpdatePauseState(op);
                }
                else if (op == SyncTimerOperation.Stop)
                {
                    float prev = Remaining;
                    StopTimer_Internal(asServer);
                    InvokeOnChange(op, prev, 0f, false);
                }
                //
                else if (op == SyncTimerOperation.StopUpdated)
                {
                    float prev = Remaining;
                    float next = reader.ReadSingle();
                    StopTimer_Internal(asServer);
                    InvokeOnChange(op, prev, next, asServer);
                }
            }

            //Updates a pause state with a pause or unpause operation.
            void UpdatePauseState(SyncTimerOperation op)
            {
                bool newPauseState = (op == SyncTimerOperation.Pause || op == SyncTimerOperation.PauseUpdated);

                float prev = Remaining;
                float next;
                //If updated time as well.
                if (op == SyncTimerOperation.PauseUpdated)
                {
                    next = reader.ReadSingle();
                    if (CanSetValues(asServer))
                        Remaining = next;
                }
                else
                {
                    next = Remaining;
                }

                if (CanSetValues(asServer))
                    Paused = newPauseState;
                InvokeOnChange(op, prev, next, asServer);
            }

            if (changes > 0)
                InvokeOnChange(SyncTimerOperation.Complete, -1f, -1f, false);
        }

        /// <summary>
        /// Stops the timer and resets.
        /// </summary>
        private void StopTimer_Internal(bool asServer)
        {
            if (!CanSetValues(asServer))
                return;

            Paused = false;
            Remaining = 0f;
        }


        /// <summary>
        /// Invokes OnChanged callback.
        /// </summary>
        private void InvokeOnChange(SyncTimerOperation operation, float prev, float next, bool asServer)
        {
            if (asServer)
            {
                if (base.NetworkBehaviour.OnStartServerCalled)
                    OnChange?.Invoke(operation, prev, next, asServer);
                else
                    _serverOnChanges.Add(new ChangeData(operation, prev, next));
            }
            else
            {
                if (base.NetworkBehaviour.OnStartClientCalled)
                    OnChange?.Invoke(operation, prev, next, asServer);
                else
                    _clientOnChanges.Add(new ChangeData(operation, prev, next));
            }
        }


        /// <summary>
        /// Called after OnStartXXXX has occurred.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        public override void OnStartCallback(bool asServer)
        {
            base.OnStartCallback(asServer);
            List<ChangeData> collection = (asServer) ? _serverOnChanges : _clientOnChanges;

            if (OnChange != null)
            {
                foreach (ChangeData item in collection)
                    OnChange.Invoke(item.Operation, item.Previous, item.Next, asServer);
            }

            collection.Clear();
        }

        /// <summary>
        /// Removes delta from Remaining for server and client.
        /// </summary>
        /// <param name="delta">Value to remove from Remaining.</param>
        public void Update(float delta)
        {
            //Not enabled.
            if (Remaining <= 0f)
                return;
            if (Paused)
                return;

            if (delta < 0)
                delta *= -1f;
            float prev = Remaining;
            Remaining -= delta;
            //Still time left.
            if (Remaining > 0f)
                return;

            /* If here then the timer has
             * ended. Invoking the events is tricky
             * here because both the server and the client
             * would share the same value. Because of this check
             * if each socket is started and if so invoke for that
             * side. There's a chance down the road this may need to be improved
             * for some but at this time I'm unable to think of any
             * problems. */
            Remaining = 0f;
            if (base.NetworkManager.IsServer)
                OnChange?.Invoke(SyncTimerOperation.Finished, prev, 0f, true);
            if (base.NetworkManager.IsClient)
                OnChange?.Invoke(SyncTimerOperation.Finished, prev, 0f, false);
        }

        /// <summary>
        /// Return the serialized type.
        /// </summary>
        /// <returns></returns>
        public object GetSerializedType() => null;
    }
}
