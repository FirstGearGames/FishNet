using FishNet.CodeGenerating;
using FishNet.Component.Prediction;
using FishNet.Managing;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Object.Prediction
{
    public static class PredictionRigidbody2D2DSerializers
    {
        public static void WriteForceData(this Writer w, PredictionRigidbody2D.EntryData value)
        {
            PredictionRigidbody2D.ForceApplicationType appType = value.Type;
            w.WriteUInt8Unpacked((byte)appType);
            PredictionRigidbody2D.AllForceData data = value.Data;

            switch (appType)
            {
                case PredictionRigidbody2D.ForceApplicationType.AddForce:
                case PredictionRigidbody2D.ForceApplicationType.AddRelativeForce:
                    w.WriteVector3(data.Vector3Force);
                    w.WriteInt32((byte)data.Mode);
                    break;
                case PredictionRigidbody2D.ForceApplicationType.AddTorque:
                    w.WriteSingle(data.FloatForce);
                    w.WriteInt32((byte)data.Mode);
                    break;
                case PredictionRigidbody2D.ForceApplicationType.AddForceAtPosition:
                    w.WriteVector3(data.Vector3Force);
                    w.WriteVector3(data.Position);
                    w.WriteInt32((byte)data.Mode);
                    break;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    break;
            }
        }

        public static PredictionRigidbody2D.EntryData ReadForceData(this Reader r)
        {
            PredictionRigidbody2D.EntryData fd = new PredictionRigidbody2D.EntryData();

            PredictionRigidbody2D.ForceApplicationType appType = (PredictionRigidbody2D.ForceApplicationType)r.ReadUInt8Unpacked();
            fd.Type = appType;

            PredictionRigidbody2D.AllForceData data = new();

            switch (appType)
            {
                case PredictionRigidbody2D.ForceApplicationType.AddForce:
                case PredictionRigidbody2D.ForceApplicationType.AddRelativeForce:
                    data.Vector3Force = r.ReadVector3();
                    data.Mode = (ForceMode2D)r.ReadUInt8Unpacked();
                    return fd;
                case PredictionRigidbody2D.ForceApplicationType.AddTorque:
                    data.FloatForce = r.ReadSingle();
                    data.Mode = (ForceMode2D)r.ReadUInt8Unpacked();
                    return fd;
                case PredictionRigidbody2D.ForceApplicationType.AddForceAtPosition:
                    data.Vector3Force = r.ReadVector3();
                    data.Position = r.ReadVector3();
                    data.Mode = (ForceMode2D)r.ReadUInt8Unpacked();
                    return fd;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    return fd;
            }
        }

        public static void WritePredictionRigidbody2D(this Writer w, PredictionRigidbody2D pr)
        {
            w.Write(pr.Rigidbody2D.GetState());
            w.WriteList<PredictionRigidbody2D.EntryData>(pr.GetPendingForces());
        }

        public static PredictionRigidbody2D ReadPredictionRigidbody2D(this Reader r)
        {
            List<PredictionRigidbody2D.EntryData> lst = CollectionCaches<PredictionRigidbody2D.EntryData>.RetrieveList();
            Rigidbody2DState rs = r.Read<Rigidbody2DState>();
            r.ReadList<PredictionRigidbody2D.EntryData>(ref lst);
            PredictionRigidbody2D pr = ResettableObjectCaches<PredictionRigidbody2D>.Retrieve();

            pr.SetReconcileData(rs, lst);
            pr.SetPendingForces(lst);
            return pr;
        }

    }

    [UseGlobalCustomSerializer]
    [Preserve]
    public class PredictionRigidbody2D : IResettable
    {
        #region Types.
        //How the force was applied.
        [System.Flags]
        public enum ForceApplicationType : byte
        {
            AddForceAtPosition = 1,
            AddForce = 4,
            AddRelativeForce = 8,
            AddTorque = 16,
        }
        public struct AllForceData
        {
            public Vector3 Vector3Force;
            public float FloatForce;
            public Vector3 Position;
            public ForceMode2D Mode;

            public AllForceData(Vector3 force, ForceMode2D mode) : this()
            {
                Vector3Force = force;
                Mode = mode;
            }


            public AllForceData(float force, ForceMode2D mode) : this()
            {
                FloatForce = force;
                Mode = mode;
            }

            public AllForceData(Vector3 force, Vector3 position, ForceMode2D mode) : this()
            {
                Vector3Force = force;
                Position = position;
                Mode = mode;
            }
        }


        [UseGlobalCustomSerializer]
        public struct EntryData
        {
            public ForceApplicationType Type;
            public AllForceData Data;

            public EntryData(ForceApplicationType type, AllForceData data)
            {
                Type = type;
                Data = data;
            }
            public EntryData(EntryData fd)
            {
                Type = fd.Type;
                Data = fd.Data;
            }
        }
        #endregion

        #region Internal.
        /// <summary>
        /// Rigidbody2DState set only as reconcile data.
        /// </summary>
        [System.NonSerialized]
        internal Rigidbody2DState Rigidbody2DState;
        #endregion

        #region Public.
        /// <summary>
        /// Rigidbody which force is applied.
        /// </summary>
        public Rigidbody2D Rigidbody2D { get; private set; }
        /// <summary>
        /// Returns if there are any pending forces.
        /// </summary>
        public bool HasPendingForces => (_pendingForces != null && _pendingForces.Count > 0);
        #endregion

        #region Private
        /// <summary>
        /// Forces waiting to be applied.
        /// </summary>
        [ExcludeSerialization]
        private List<EntryData> _pendingForces;
        /// <summary>
        /// Returns current pending forces.
        /// Modifying this collection could cause undesirable results.
        /// </summary>
        public List<EntryData> GetPendingForces() => _pendingForces;
        #endregion

        ~PredictionRigidbody2D()
        {
            if (_pendingForces != null)
                CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
            Rigidbody2D = null;
        }

        /// <summary>
        /// Rigidbody which force is applied.
        /// </summary>
        /// <param name="rb"></param>
        public void Initialize(Rigidbody2D rb)
        {
            Rigidbody2D = rb;
            if (_pendingForces == null)
                _pendingForces = CollectionCaches<EntryData>.RetrieveList();
            else
                _pendingForces.Clear();
        }

        /// <summary>
        /// Adds Velocity force to the Rigidbody.
        /// </summary>
        public void AddForce(Vector3 force, ForceMode2D mode = ForceMode2D.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddForce,
                new AllForceData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddRelativeForce(Vector3 force, ForceMode2D mode = ForceMode2D.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddRelativeForce,
                new AllForceData(force, mode));
            _pendingForces.Add(fd);

        }
        public void AddTorque(float force, ForceMode2D mode = ForceMode2D.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddTorque,
                new AllForceData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode2D mode = ForceMode2D.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddForceAtPosition,
                new AllForceData(force, position, mode));
            _pendingForces.Add(fd);
        }

        /// <summary>
        /// Sets velocity while clearing pending forces.
        /// Simulate should still be called normally.
        /// </summary>
        public void Velocity(Vector3 force)
        {
            Rigidbody2D.velocity = force;
            RemoveForces(true);
        }

        /// <summary>
        /// Sets angularVelocity while clearning pending forces.
        /// Simulate should still be called normally.
        /// </summary>
        public void AngularVelocity(float force)
        {
            Rigidbody2D.angularVelocity = force;
            RemoveForces(false);
        }

        /// <summary>
        /// Applies pending forces to rigidbody in the order they were added.
        /// </summary>
        public void Simulate()
        {
            foreach (EntryData item in _pendingForces)
            {
                AllForceData data = item.Data;
                switch (item.Type)
                {
                    case ForceApplicationType.AddTorque:
                        Rigidbody2D.AddTorque(data.FloatForce, data.Mode);
                        break;
                    case ForceApplicationType.AddForce:
                        Rigidbody2D.AddForce(data.Vector3Force, data.Mode);
                        break;
                    case ForceApplicationType.AddRelativeForce:
                        Rigidbody2D.AddRelativeForce(data.Vector3Force, data.Mode);
                        break;
                    case ForceApplicationType.AddForceAtPosition:
                        Rigidbody2D.AddForceAtPosition(data.Vector3Force, data.Position, data.Mode);
                        break;
                }
            }
            _pendingForces.Clear();
        }

        /// <summary>
        /// Manually clears pending forces.
        /// </summary>
        /// <param name="velocity">True to clear velocities, false to clear angular velocities.</param>
        public void ClearPendingForces(bool velocity)
        {
            RemoveForces(velocity);
        }
        /// <summary>
        /// Clears pending velocity and angular velocity forces.
        /// </summary>
        public void ClearPendingForces()
        {
            _pendingForces.Clear();
        }

        /// <summary>
        /// Reconciles to a state.
        /// </summary>
        public void Reconcile(PredictionRigidbody2D pr)
        {
            _pendingForces.Clear();
            if (pr._pendingForces != null)
            {
                foreach (EntryData item in pr._pendingForces)
                    _pendingForces.Add(new EntryData(item));
            }
            Rigidbody2D.SetState(pr.Rigidbody2DState);

            ResettableObjectCaches<PredictionRigidbody2D>.Store(pr);
        }

        /// <summary>
        /// Removes forces from pendingForces.
        /// </summary>
        /// <param name="velocity">True to remove if velocity, false if to remove angular velocity.</param>
        private void RemoveForces(bool velocity)
        {
            if (_pendingForces.Count > 0)
            {
                bool shouldExist = velocity;
                ForceApplicationType velocityApplicationTypes = (ForceApplicationType.AddRelativeForce | ForceApplicationType.AddForce);

                List<EntryData> newDatas = CollectionCaches<EntryData>.RetrieveList();
                foreach (EntryData item in _pendingForces)
                {
                    if (VelocityApplicationTypesContains(item.Type) == !velocity)
                        newDatas.Add(item);
                }
                //Add back to _pendingForces if changed.
                if (newDatas.Count != _pendingForces.Count)
                {
                    _pendingForces.Clear();
                    foreach (EntryData item in newDatas)
                        _pendingForces.Add(item);
                }
                CollectionCaches<EntryData>.Store(newDatas);

                bool VelocityApplicationTypesContains(ForceApplicationType apt)
                {
                    return (velocityApplicationTypes & apt) == apt;
                }
            }


        }

        internal void SetPendingForces(List<EntryData> lst) => _pendingForces = lst;

        internal void SetReconcileData(Rigidbody2DState rs, List<EntryData> lst)
        {
            Rigidbody2DState = rs;
            _pendingForces = lst;
        }

        public void ResetState()
        {
            CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
            Rigidbody2D = null;
        }

        public void InitializeState() { }
    }

}

