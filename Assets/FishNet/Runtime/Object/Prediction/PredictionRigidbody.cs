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
    [Preserve]
    public static class PredictionRigidbodySerializers
    {
        public static void WriteEntryData(this Writer w, PredictionRigidbody.EntryData value)
        {
            PredictionRigidbody.ForceApplicationType appType = value.Type;
            w.WriteUInt8Unpacked((byte)appType);
            PredictionRigidbody.AllForceData data = value.Data;

            switch (appType)
            {
                case PredictionRigidbody.ForceApplicationType.AddTorque:
                case PredictionRigidbody.ForceApplicationType.AddForce:
                case PredictionRigidbody.ForceApplicationType.AddRelativeTorque:
                case PredictionRigidbody.ForceApplicationType.AddRelativeForce:
                    w.WriteVector3(data.Vector3Force);
                    w.WriteInt32((byte)data.Mode);
                    break;
                case PredictionRigidbody.ForceApplicationType.AddExplosiveForce:
                    w.WriteSingle(data.FloatForce);
                    w.WriteVector3(data.Position);
                    w.WriteSingle(data.Radius);
                    w.WriteSingle(data.UpwardsModifier);
                    w.WriteInt32((byte)data.Mode);
                    break;
                case PredictionRigidbody.ForceApplicationType.AddForceAtPosition:
                    w.WriteVector3(data.Vector3Force);
                    w.WriteVector3(data.Position);
                    w.WriteInt32((byte)data.Mode);
                    break;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    break;
            }
        }

        public static PredictionRigidbody.EntryData ReadEntryData(this Reader r)
        {
            PredictionRigidbody.EntryData fd = new PredictionRigidbody.EntryData();

            PredictionRigidbody.ForceApplicationType appType = (PredictionRigidbody.ForceApplicationType)r.ReadUInt8Unpacked();
            fd.Type = appType;

            PredictionRigidbody.AllForceData data = new();
            
            switch (appType)
            {
                case PredictionRigidbody.ForceApplicationType.AddTorque:
                case PredictionRigidbody.ForceApplicationType.AddForce:
                case PredictionRigidbody.ForceApplicationType.AddRelativeTorque:
                case PredictionRigidbody.ForceApplicationType.AddRelativeForce:
                    data.Vector3Force = r.ReadVector3();
                    data.Mode = (ForceMode)r.ReadInt32();
                    break;
                case PredictionRigidbody.ForceApplicationType.AddExplosiveForce:
                    data.FloatForce = r.ReadSingle();
                    data.Position = r.ReadVector3();
                    data.Radius = r.ReadSingle();
                    data.UpwardsModifier = r.ReadSingle();
                    data.Mode = (ForceMode)r.ReadInt32();
                    break;
                case PredictionRigidbody.ForceApplicationType.AddForceAtPosition:
                    data.Vector3Force = r.ReadVector3();
                    data.Position = r.ReadVector3();
                    data.Mode = (ForceMode)r.ReadInt32();
                    break;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    break;
            }

            fd.Data = data;
            return fd;
        }

        public static void WritePredictionRigidbody(this Writer w, PredictionRigidbody pr)
        {
            w.Write(pr.Rigidbody.GetState());
            w.WriteList(pr.GetPendingForces());
        }

        public static PredictionRigidbody ReadPredictionRigidbody(this Reader r)
        {

            List<PredictionRigidbody.EntryData> lst = CollectionCaches<PredictionRigidbody.EntryData>.RetrieveList();

            RigidbodyState rs = r.Read<RigidbodyState>();
            r.ReadList(ref lst);
            PredictionRigidbody pr = ResettableObjectCaches<PredictionRigidbody>.Retrieve();

            pr.SetReconcileData(rs, lst);
            return pr;
        }

    }

    [UseGlobalCustomSerializer]
    [Preserve]
    public class PredictionRigidbody : IResettable
    {
        #region Types.
        public struct AllForceData
        {
            public ForceMode Mode;
            public Vector3 Vector3Force;
            public Vector3 Position;
            public float FloatForce;
            public float Radius;
            public float UpwardsModifier;

            /// <summary>
            /// Used for Force and Torque.
            /// </summary>
            public AllForceData(Vector3 force, ForceMode mode) : this()
            {
                Vector3Force = force;
                Mode = mode;
            }

            /// <summary>
            /// Used for Position.
            /// </summary>
            public AllForceData(Vector3 force, Vector3 position, ForceMode mode) : this()
            {
                Vector3Force = force;
                Position = position;
                Mode = mode;
            }

            /// <summary>
            /// Used for Explosive.
            /// </summary>
            /// <param name="force"></param>
            /// <param name="position"></param>
            /// <param name="radius"></param>
            /// <param name="upwardsModifier"></param>
            /// <param name="mode"></param>
            public AllForceData(float force, Vector3 position, float radius, float upwardsModifier, ForceMode mode) : this()
            {
                FloatForce = force;
                Position = position;
                Radius = radius;
                UpwardsModifier = upwardsModifier;
                Mode = mode;
            }
        }
        public interface IForceData { }
        //How the force was applied.
        [System.Flags]
        public enum ForceApplicationType : byte
        {
            AddForceAtPosition = 1,
            AddExplosiveForce = 2,
            AddForce = 4,
            AddRelativeForce = 8,
            AddTorque = 16,
            AddRelativeTorque = 32,
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

        #region Public.
        /// <summary>
        /// Rigidbody which force is applied.
        /// </summary>
        public Rigidbody Rigidbody { get; private set; }
        /// <summary>
        /// Returns if there are any pending forces.
        /// </summary>
        public bool HasPendingForces => (_pendingForces != null && _pendingForces.Count > 0);
        #endregion

        #region Internal.
        /// <summary>
        /// RigidbodyState set only as reconcile data.
        /// </summary>
        [System.NonSerialized]
        internal RigidbodyState RigidbodyState;
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

        ~PredictionRigidbody()
        {
            if (_pendingForces != null)
                CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
            Rigidbody = null;
        }

        /// <summary>
        /// Rigidbody which force is applied.
        /// </summary>
        /// <param name="rb"></param>
        public void Initialize(Rigidbody rb)
        {
            Rigidbody = rb;
            if (_pendingForces == null)
                _pendingForces = CollectionCaches<EntryData>.RetrieveList();
            else
                _pendingForces.Clear();
        }

        /// <summary>
        /// Adds Velocity force to the Rigidbody.
        /// </summary>
        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddForce,
                new AllForceData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddRelativeForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddRelativeForce,
                new AllForceData(force, mode));
            _pendingForces.Add(fd);

        }
        public void AddTorque(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddTorque,
                new AllForceData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddRelativeTorque(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddRelativeTorque,
                new AllForceData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddExplosiveForce(float force, Vector3 position, float radius, float upwardsModifier = 0f, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddExplosiveForce,
                new AllForceData(force, position, radius, upwardsModifier, mode));
            _pendingForces.Add(fd);
        }
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force)
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
            Rigidbody.velocity = force;
            RemoveForces(true);
        }

        /// <summary>
        /// Sets angularVelocity while clearning pending forces.
        /// Simulate should still be called normally.
        /// </summary>
        public void AngularVelocity(Vector3 force)
        {
            Rigidbody.angularVelocity = force;
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
                        Rigidbody.AddTorque(data.Vector3Force, data.Mode);
                        break;
                    case ForceApplicationType.AddForce:
                        Rigidbody.AddForce(data.Vector3Force, data.Mode);
                        break;
                    case ForceApplicationType.AddRelativeTorque:                        
                        Rigidbody.AddRelativeTorque(data.Vector3Force, data.Mode);
                        break;
                    case ForceApplicationType.AddRelativeForce:
                        Rigidbody.AddRelativeForce(data.Vector3Force, data.Mode);
                        break;
                    case ForceApplicationType.AddExplosiveForce:
                        Rigidbody.AddExplosionForce(data.FloatForce, data.Position, data.Radius, data.UpwardsModifier, data.Mode);
                        break;
                    case ForceApplicationType.AddForceAtPosition:
                        Rigidbody.AddForceAtPosition(data.Vector3Force, data.Position, data.Mode);
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
        public void Reconcile(PredictionRigidbody pr)
        {
            _pendingForces.Clear();
            if (pr._pendingForces != null)
            {
                foreach (EntryData item in pr._pendingForces)
                    _pendingForces.Add(new EntryData(item));
            }
            //Set state.
            Rigidbody.SetState(pr.RigidbodyState);

            ResettableObjectCaches<PredictionRigidbody>.Store(pr);
        }

        /// <summary>
        /// Removes forces from pendingForces.
        /// </summary>
        /// <param name="velocity">True to remove if velocity, false if to remove angular velocity.</param>
        private void RemoveForces(bool velocity)
        {
            if (_pendingForces.Count > 0)
            {
                ForceApplicationType velocityApplicationTypes = (ForceApplicationType.AddRelativeForce | ForceApplicationType.AddForce | ForceApplicationType.AddExplosiveForce);

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

        internal void SetReconcileData(RigidbodyState rs, List<EntryData> lst)
        {
            RigidbodyState = rs;
            _pendingForces = lst;
        }

        public void ResetState()
        {
            CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
            Rigidbody = null;
        }

        public void InitializeState() { }
    }

}

