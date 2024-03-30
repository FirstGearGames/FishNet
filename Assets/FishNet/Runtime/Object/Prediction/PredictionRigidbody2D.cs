using FishNet.CodeGenerating;
using FishNet.Managing;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object.Prediction
{
#if PREDICTION_V2

    public static class PredictionRigidbody2D2DSerializers
    {
        public static void WriteForceData(this Writer w, PredictionRigidbody2D.EntryData value)
        {
            PredictionRigidbody2D.ForceApplicationType appType = value.Type;
            w.WriteByte((byte)appType);
            switch (appType)
            {
                case PredictionRigidbody2D.ForceApplicationType.AddForce:
                case PredictionRigidbody2D.ForceApplicationType.AddRelativeForce:
                    w.Write((PredictionRigidbody2D.ForceData)value.Data);
                    break;
                case PredictionRigidbody2D.ForceApplicationType.AddTorque:
                    w.Write((PredictionRigidbody2D.TorqueData)value.Data);
                    break;
                case PredictionRigidbody2D.ForceApplicationType.AddForceAtPosition:
                    w.Write((PredictionRigidbody2D.PositionForceData)value.Data);
                    break;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    break;
            }
        }

        public static PredictionRigidbody2D.EntryData ReadForceData(this Reader r)
        {
            PredictionRigidbody2D.EntryData fd = new PredictionRigidbody2D.EntryData();

            PredictionRigidbody2D.ForceApplicationType appType = (PredictionRigidbody2D.ForceApplicationType)r.ReadByte();
            fd.Type = appType;

            switch (appType)
            {
                case PredictionRigidbody2D.ForceApplicationType.AddForce:
                case PredictionRigidbody2D.ForceApplicationType.AddRelativeForce:
                    fd.Data = r.Read<PredictionRigidbody2D.ForceData>();
                    return fd;
                case PredictionRigidbody2D.ForceApplicationType.AddTorque:
                    fd.Data = r.Read<PredictionRigidbody2D.TorqueData>();
                    return fd;
                case PredictionRigidbody2D.ForceApplicationType.AddForceAtPosition:
                    fd.Data = r.Read<PredictionRigidbody2D.PositionForceData>();
                    return fd;
                default:
                    NetworkManagerExtensions.LogError($"ForceApplicationType of {appType} is not supported.");
                    return fd;
            }



        }

        public static void WritePredictionRigidbody2D(this Writer w, PredictionRigidbody2D pr)
        {
            w.WriteList<PredictionRigidbody2D.EntryData>(pr.GetPendingForces());
        }

        public static PredictionRigidbody2D ReadPredictionRigidbody2D(this Reader r)
        {
            List<PredictionRigidbody2D.EntryData> lst = CollectionCaches<PredictionRigidbody2D.EntryData>.RetrieveList();
            r.ReadList<PredictionRigidbody2D.EntryData>(ref lst);
            PredictionRigidbody2D pr = ResettableObjectCaches<PredictionRigidbody2D>.Retrieve();

            pr.SetPendingForces(lst);
            return pr;
        }

    }

    [UseGlobalCustomSerializer]
    public class PredictionRigidbody2D : IResettable
    {
        #region Types.
        public interface IForceData { }
        //How the force was applied.
        [System.Flags]
        public enum ForceApplicationType : byte
        {
            AddForceAtPosition = 1,
            AddForce = 4,
            AddRelativeForce = 8,
            AddTorque = 16,
        }
        public struct ForceData : IForceData
        {
            public Vector3 Force;
            public ForceMode2D Mode;

            public ForceData(Vector3 force, ForceMode2D mode)
            {
                Force = force;
                Mode = mode;
            }
        }
        public struct TorqueData : IForceData
        {
            public float Force;
            public ForceMode2D Mode;

            public TorqueData(float force, ForceMode2D mode)
            {
                Force = force;
                Mode = mode;
            }
        }
        public struct PositionForceData : IForceData
        {
            public Vector3 Force;
            public Vector3 Position;
            public ForceMode2D Mode;

            public PositionForceData(Vector3 force, Vector3 position, ForceMode2D mode)
            {
                Force = force;
                Position = position;
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
        public Rigidbody2D Rigidbody2D { get; private set; }
        #endregion

        #region Private
        /// <summary>
        /// Forces waiting to be applied.
        /// </summary>
        [ExcludeSerialization]
        private List<EntryData> _pendingForces;
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
                new ForceData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddRelativeForce(Vector3 force, ForceMode2D mode = ForceMode2D.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddRelativeForce,
                new ForceData(force, mode));
            _pendingForces.Add(fd);

        }
        public void AddTorque(Vector3 force, ForceMode2D mode = ForceMode2D.Force)
        {
            EntryData fd = new EntryData(ForceApplicationType.AddTorque,
                new ForceData(force, mode));
            _pendingForces.Add(fd);
        }
        public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode2D mode = ForceMode2D.Force)
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
                switch (item.Type)
                {
                    case ForceApplicationType.AddTorque:
                        TorqueData e0 = (TorqueData)item.Data;
                        Rigidbody2D.AddTorque(e0.Force, e0.Mode);
                        break;
                    case ForceApplicationType.AddForce:
                        ForceData e1 = (ForceData)item.Data;
                        Rigidbody2D.AddForce(e1.Force, e1.Mode);
                        break;
                    case ForceApplicationType.AddRelativeForce:
                        ForceData e3 = (ForceData)item.Data;
                        Rigidbody2D.AddRelativeForce(e3.Force, e3.Mode);
                        break;
                    case ForceApplicationType.AddForceAtPosition:
                        PositionForceData e5 = (PositionForceData)item.Data;
                        Rigidbody2D.AddForceAtPosition(e5.Force, e5.Position, e5.Mode);
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

        internal List<EntryData> GetPendingForces() => _pendingForces;
        internal void SetPendingForces(List<EntryData> lst) => _pendingForces = lst;

        public void ResetState()
        {
            CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
            Rigidbody2D = null;
        }

        public void InitializeState() { }
    }
#endif

}

