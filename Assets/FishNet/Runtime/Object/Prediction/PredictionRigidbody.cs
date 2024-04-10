using FishNet.CodeGenerating;
using FishNet.Managing;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object.Prediction
{
#if !PREDICTION_1

    public static class PredictionRigidbodySerializers
    {
        public static void WriteForceData(this Writer w, PredictionRigidbody.EntryData value)
        {
            PredictionRigidbody.ForceApplicationType appType = value.Type;
            w.WriteByte((byte)appType);
            switch (appType)
            {
                case PredictionRigidbody.ForceApplicationType.AddTorque:
                case PredictionRigidbody.ForceApplicationType.AddForce:
                case PredictionRigidbody.ForceApplicationType.AddRelativeTorque:
                case PredictionRigidbody.ForceApplicationType.AddRelativeForce:
                    w.Write((PredictionRigidbody.ForceAndTorqueData)value.Data);
                    break;
                case PredictionRigidbody.ForceApplicationType.AddExplosiveForce:
                    w.Write((PredictionRigidbody.ExplosiveForceData)value.Data);
                    break;
                case PredictionRigidbody.ForceApplicationType.AddForceAtPosition:
                    w.Write((PredictionRigidbody.PositionForceData)value.Data);
                    break;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    break;
            }
        }

        public static PredictionRigidbody.EntryData ReadForceData(this Reader r)
        {
            PredictionRigidbody.EntryData fd = new PredictionRigidbody.EntryData();

            PredictionRigidbody.ForceApplicationType appType = (PredictionRigidbody.ForceApplicationType)r.ReadByte();
            fd.Type = appType;

            switch (appType)
            {
                case PredictionRigidbody.ForceApplicationType.AddTorque:
                case PredictionRigidbody.ForceApplicationType.AddForce:
                case PredictionRigidbody.ForceApplicationType.AddRelativeTorque:
                case PredictionRigidbody.ForceApplicationType.AddRelativeForce:
                    fd.Data = r.Read<PredictionRigidbody.ForceAndTorqueData>();
                    return fd;
                case PredictionRigidbody.ForceApplicationType.AddExplosiveForce:
                    fd.Data = r.Read<PredictionRigidbody.ExplosiveForceData>();
                    return fd;
                case PredictionRigidbody.ForceApplicationType.AddForceAtPosition:
                    fd.Data = r.Read<PredictionRigidbody.PositionForceData>();
                    return fd;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    return fd;
            }



        }

        public static void WritePredictionRigidbody(this Writer w, PredictionRigidbody pr)
        {
            w.WriteList<PredictionRigidbody.EntryData>(pr.GetPendingForces());
        }

        public static PredictionRigidbody ReadPredictionRigidbody(this Reader r)
        {
            List<PredictionRigidbody.EntryData> lst = CollectionCaches<PredictionRigidbody.EntryData>.RetrieveList();
            r.ReadList<PredictionRigidbody.EntryData>(ref lst);
            PredictionRigidbody pr = ResettableObjectCaches<PredictionRigidbody>.Retrieve();

            pr.SetPendingForces(lst);
            return pr;
        }

    }

    [UseGlobalCustomSerializer]
    public class PredictionRigidbody : IResettable
    {
        #region Types.
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
        public struct ForceAndTorqueData : IForceData
        {
            public Vector3 Force;
            public ForceMode Mode;

            public ForceAndTorqueData(Vector3 force, ForceMode mode)
            {
                Force = force;
                Mode = mode;
            }
        }
        public struct PositionForceData : IForceData
        {
            public Vector3 Force;
            public Vector3 Position;
            public ForceMode Mode;

            public PositionForceData(Vector3 force, Vector3 position, ForceMode mode)
            {
                Force = force;
                Position = position;
                Mode = mode;
            }
        }
        public struct ExplosiveForceData : IForceData
        {
            public float Force;
            public Vector3 Position;
            public float Radius;
            public float UpwardsModifier;
            public ForceMode Mode;

            public ExplosiveForceData(float force, Vector3 position, float radius, float upwardsModifier, ForceMode mode)
            {
                Force = force;
                Position = position;
                Radius = radius;
                UpwardsModifier = upwardsModifier;
                Mode = mode;
            }
        }

        [UseGlobalCustomSerializer]
        public struct EntryData
        {
            public ForceApplicationType Type;
            public IForceData Data;

            public EntryData(ForceApplicationType type, IForceData data)
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
        #endregion

        #region Private
        /// <summary>
        /// Forces waiting to be applied.
        /// </summary>
        [ExcludeSerialization]
        private List<EntryData> _pendingForces;
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
                new ForceAndTorqueData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddRelativeForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddRelativeForce,
                new ForceAndTorqueData(force, mode));
            _pendingForces.Add(fd);

        }
        public void AddTorque(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddTorque,
                new ForceAndTorqueData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddRelativeTorque(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddRelativeTorque,
                new ForceAndTorqueData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddExplosiveForce(float force, Vector3 position, float radius, float upwardsModifier = 0f,  ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddExplosiveForce,
                new ExplosiveForceData(force, position, radius, upwardsModifier, mode));
            _pendingForces.Add(fd);
        }
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddForceAtPosition,
                new PositionForceData(force, position, mode));
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
                switch (item.Type)
                {
                    case ForceApplicationType.AddTorque:
                        ForceAndTorqueData e0 = (ForceAndTorqueData)item.Data;
                        Rigidbody.AddTorque(e0.Force, e0.Mode);
                        break;
                    case ForceApplicationType.AddForce:
                        ForceAndTorqueData e1 = (ForceAndTorqueData)item.Data;
                        Rigidbody.AddForce(e1.Force, e1.Mode);
                        break;
                    case ForceApplicationType.AddRelativeTorque:
                        ForceAndTorqueData e2 = (ForceAndTorqueData)item.Data;
                        Rigidbody.AddRelativeTorque(e2.Force, e2.Mode);
                        break;
                    case ForceApplicationType.AddRelativeForce:
                        ForceAndTorqueData e3 = (ForceAndTorqueData)item.Data;
                        Rigidbody.AddRelativeForce(e3.Force, e3.Mode);
                        break;
                    case ForceApplicationType.AddExplosiveForce:
                        ExplosiveForceData e4 = (ExplosiveForceData)item.Data;
                        Rigidbody.AddExplosionForce(e4.Force, e4.Position, e4.Radius, e4.UpwardsModifier, e4.Mode);
                        break;
                    case ForceApplicationType.AddForceAtPosition:
                        PositionForceData e5 = (PositionForceData)item.Data;
                        Rigidbody.AddForceAtPosition(e5.Force, e5.Position, e5.Mode);
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
                bool shouldExist = velocity;
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

        internal List<EntryData> GetPendingForces() => _pendingForces;
        internal void SetPendingForces(List<EntryData> lst) => _pendingForces = lst;

        public void ResetState()
        {
            CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
            Rigidbody = null;
        }

        public void InitializeState() { }
    }
#endif

}

