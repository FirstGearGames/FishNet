using FishNet.Managing.Logging;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Example.CustomSyncObject
{
    /// <summary>
    /// This is the data type I want to create a custom SyncType for.
    /// </summary>
    public struct Structy
    {
        public string Name;
        public ushort Age;

        public Structy(string name, ushort age)
        {
            Name = name;
            Age = age;
        }
    }

    /// <summary>
    /// It's very important to exclude this from codegen.
    /// However, whichever value you are synchronizing must not be excluded. This is why the value is outside the StructySync class.
    /// </summary>
    public class StructySync : SyncBase, ICustomSync
    {
        #region Types.
        /// <summary>
        /// Information about how the struct has changed.
        /// You could send the entire struct on every change
        /// but this is an example of how you might send individual changed
        /// fields.
        /// </summary>
        private struct ChangeData
        {
            internal CustomOperation Operation;
            internal Structy Data;

            public ChangeData(CustomOperation operation, Structy data)
            {
                Operation = operation;
                Data = data;
            }
        }
        /// <summary>
        /// Types of changes. This is related to ChangedData
        /// where you can specify what has changed.
        /// </summary>
        public enum CustomOperation : byte
        {
            Full = 0,
            Name = 1,
            Age = 2
        }
        #endregion

        #region Public.
        /// <summary>
        /// Delegate signature for when Structy changes.
        /// </summary>
        /// <param name="op"></param>
        /// <param name="oldItem"></param>
        /// <param name="newItem"></param>
        public delegate void CustomChanged(CustomOperation op, Structy oldItem, Structy newItem, bool asServer);
        /// <summary>
        /// Called when the Structy changes.
        /// </summary>
        public event CustomChanged OnChange;
        #endregion

        #region Private.
        /// <summary>
        /// Initial value when initialized.
        /// </summary>
        private Structy _initialValue;
        /// <summary>
        /// Value this SyncType is for, which is Structy.
        /// </summary>
        private Structy _value = new Structy();
        /// <summary>
        /// Copy of value on client portion when acting as a host.
        /// This is not mandatory but this setup separates server values
        /// from client, creating a more reliable test environment when running as host.
        /// </summary>
        private Structy _clientValue = new Structy();
        /// <summary>
        /// Changed data which will be sent next tick.
        /// </summary>
        private readonly List<ChangeData> _changed = new List<ChangeData>();
        /// <summary>
        /// True if values have changed since initialization.
        /// The only reasonable way to reset this during a Reset call is by duplicating the original list and setting all values to it on reset.
        /// </summary>
        private bool _valuesChanged;
        #endregion


        protected override void Registered()
        {
            base.Registered();
            _initialValue = _value;
        }

        /// <summary>
        /// Adds an operation and invokes locally.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="index"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        private void AddOperation(CustomOperation operation, Structy prev, Structy next)
        {
            if (!base.IsRegistered)
                return;

            if (base.NetworkManager != null && base.Settings.WritePermission == WritePermission.ServerOnly && !base.NetworkBehaviour.IsServer)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Cannot complete operation as server when server is not active.");
                return;
            }

            /* Set as changed even if cannot dirty.
            * Dirty is only set when there are observers,
            * but even if there are not observers
            * values must be marked as changed so when
            * there are observers, new values are sent. */
            _valuesChanged = true;
            base.Dirty();

            //Data can currently only be set from server, so this is always asServer.
            bool asServer = true;
            //Add to changed.
            ChangeData cd = new ChangeData(operation, next);
            _changed.Add(cd);
            OnChange?.Invoke(operation, prev, next, asServer);
        }

        /// <summary>
        /// Writes all changed values.
        /// </summary>
        /// <param name="writer"></param>
        ///<param name="resetSyncTick">True to set the next time data may sync.</param>
        public override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            base.WriteDelta(writer, resetSyncTick);
            writer.WriteUInt32((uint)_changed.Count);

            for (int i = 0; i < _changed.Count; i++)
            {
                ChangeData change = _changed[i];
                writer.WriteByte((byte)change.Operation);

                //Clear does not need to write anymore data so it is not included in checks.
                if (change.Operation == CustomOperation.Age)
                {
                    writer.WriteUInt16(change.Data.Age);
                }
                else if (change.Operation == CustomOperation.Name)
                {
                    writer.WriteString(change.Data.Name);
                }
            }

            _changed.Clear();
        }

        /// <summary>
        /// Writes all values if not initial values.
        /// </summary>
        /// <param name="writer"></param>
        public override void WriteFull(PooledWriter writer)
        {
            if (!_valuesChanged)
                return;

            base.WriteHeader(writer, false);
            //Write one change.
            writer.WriteInt32(1);
            //Write if changed is from the server, so always use the server _value.           
            writer.WriteByte((byte)CustomOperation.Full);
            //Write value.
            writer.Write(_value);
        }

        /// <summary>
        /// Sets current values.
        /// </summary>
        /// <param name="reader"></param>
        public override void Read(PooledReader reader)
        {
            //Read is always on client side.
            bool asServer = false;
            /* When !asServer don't make changes if server is running.
            * This is because changes would have already been made on
            * the server side and doing so again would result in duplicates
            * and potentially overwrite data not yet sent. */
            bool asClientAndHost = (!asServer && base.NetworkManager.IsServer);

            int changes = (int)reader.ReadUInt32();
            for (int i = 0; i < changes; i++)
            {
                CustomOperation operation = (CustomOperation)reader.ReadByte();
                Structy prev = GetValue(asServer);
                Structy next = default(Structy);

                //Full.
                if (operation == CustomOperation.Full)
                {
                    next = reader.Read<Structy>();
                }
                //Name.
                else if (operation == CustomOperation.Name)
                {
                    next = prev;
                    next.Name = reader.ReadString();
                }
                //Age
                else if (operation == CustomOperation.Age)
                {
                    next = prev;
                    next.Age = reader.ReadUInt16();
                }

                OnChange?.Invoke(operation, prev, next, asServer);
            }

        }

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _changed.Clear();
            _value = _initialValue;
            _clientValue = _initialValue;
            _valuesChanged = false;
        }

        /// <summary>
        /// Sets name value.
        /// </summary>
        public void SetName(string name)
        {
            SetName(name, true, true);
        }
        private void SetName(string name, bool asServer, bool force)
        {
            Structy data = GetValue(asServer);
            bool sameValue = (!force && (name == data.Name));
            if (!sameValue)
            {
                Structy prev = data;

                Structy next = data;
                next.Name = name;
                SetValue(asServer, next);

                if (asServer)
                {
                    if (base.NetworkManager == null)
                        _clientValue = next;
                    AddOperation(CustomOperation.Name, prev, next);
                }
            }
        }

        /// <summary>
        /// Sets age value.
        /// </summary>
        public void SetAge(ushort age)
        {
            SetAge(age, true, true);
        }
        private void SetAge(ushort age, bool asServer, bool force)
        {
            Structy data = GetValue(asServer);
            bool sameValue = (!force && (age == data.Age));
            if (!sameValue)
            {
                Structy prev = data;

                Structy next = data;
                next.Age = age;
                SetValue(asServer, next);

                if (asServer)
                {
                    if (base.NetworkManager == null)
                        _clientValue = next;
                    AddOperation(CustomOperation.Age, prev, next);
                }
            }
        }

        /// <summary>
        /// Gets value depending if being called asServer or not.
        /// </summary>
        /// <param name="asServer"></param>
        /// <returns></returns>
        public Structy GetValue(bool asServer)
        {
            return (asServer) ? _value : _clientValue;
        }


        /// <summary>
        /// Sets value depending if being called asServer or not.
        /// </summary>
        /// <param name="asServer"></param>
        /// <returns></returns>
        private void SetValue(bool asServer, Structy data)
        {
            if (asServer)
                _value = data;
            else
                _clientValue = data;
        }

        /// <summary>
        /// Return the serialized type.
        /// </summary>
        /// <returns></returns>
        public object GetSerializedType() => typeof(Structy);
    }
}
