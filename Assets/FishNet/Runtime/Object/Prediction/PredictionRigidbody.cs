using FishNet.CodeGenerating;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object.Prediction
{
#if PREDICTION_V2

    public static class PredictionRigidbodySerializers
    {
        public static void WriteForceData(this Writer w, PredictionRigidbody.ForceData value)
        {
            w.WriteVector3(value.Force);
            w.Write(value.Mode);
            w.WriteBoolean(value.IsVelocity);
        }

        public static PredictionRigidbody.ForceData ReadForceData(this Reader r)
        {
            PredictionRigidbody.ForceData fd = new PredictionRigidbody.ForceData();
            fd.Force = r.ReadVector3();
            fd.Mode = r.Read<ForceMode>();
            fd.IsVelocity = r.ReadBoolean();
            return fd;
        }

        public static void WritePredictionRigidbody(this Writer w, PredictionRigidbody pr)
        {
            w.WriteList<PredictionRigidbody.ForceData>(pr.GetPendingForces());
        }

        public static PredictionRigidbody ReadPredictionRigidbody(this Reader r)
        {
            List<PredictionRigidbody.ForceData> lst = CollectionCaches<PredictionRigidbody.ForceData>.RetrieveList();
            r.ReadList<PredictionRigidbody.ForceData>(ref lst);
            PredictionRigidbody pr = ResettableObjectCaches<PredictionRigidbody>.Retrieve();

            pr.SetPendingForces(lst);
            return pr;
        }

    }

    [UseGlobalCustomSerializer]
    public class PredictionRigidbody : IResettable
    {
        #region Types.
        [UseGlobalCustomSerializer]
        public struct ForceData
        {
            public Vector3 Force;
            public ForceMode Mode;
            public bool IsVelocity;

            public ForceData(ForceData fd)
            {
                Force = fd.Force;
                Mode = fd.Mode;
                IsVelocity = fd.IsVelocity;
            }
            public ForceData(Vector3 force, ForceMode mode, bool velocity)
            {
                Force = force;
                Mode = mode;
                IsVelocity = velocity;
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
        private List<ForceData> _pendingForces;
        #endregion

        ~PredictionRigidbody()
        {
            if (_pendingForces != null)
                CollectionCaches<ForceData>.StoreAndDefault(ref _pendingForces);
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
                _pendingForces = CollectionCaches<ForceData>.RetrieveList();
            else
                _pendingForces.Clear();
        }

        /// <summary>
        /// Adds Velocity force to the Rigidbody.
        /// </summary>
        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            _pendingForces.Add(new ForceData(force, mode, true));
        }

        public void AddAngularForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            _pendingForces.Add(new ForceData(force, mode, false));
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
            foreach (ForceData item in _pendingForces)
            {
                if (item.IsVelocity)
                    Rigidbody.AddForce(item.Force, item.Mode);
                else
                    Rigidbody.AddTorque(item.Force, item.Mode);
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
                foreach (ForceData item in pr._pendingForces)
                    _pendingForces.Add(new ForceData(item));
            }

            ResettableObjectCaches<PredictionRigidbody>.Store(pr);
        }

        /// <summary>
        /// Removes forces from pendingForces.
        /// </summary>
        /// <param name="velocity">True to remove if velocity.</param>
        private void RemoveForces(bool velocity)
        {
            if (_pendingForces.Count > 0)
            {
                List<ForceData> newDatas = CollectionCaches<ForceData>.RetrieveList();
                foreach (ForceData item in _pendingForces)
                {
                    if (item.IsVelocity != velocity)
                        newDatas.Add(item);
                }
                //Add back to _pendingForces if changed.
                if (newDatas.Count != _pendingForces.Count)
                {
                    _pendingForces.Clear();
                    foreach (ForceData item in newDatas)
                        _pendingForces.Add(item);
                }
                CollectionCaches<ForceData>.Store(newDatas);
            }
        }

        internal List<ForceData> GetPendingForces() => _pendingForces;
        internal void SetPendingForces(List<ForceData> lst) => _pendingForces = lst;

        public void ResetState()
        {
            CollectionCaches<ForceData>.StoreAndDefault(ref _pendingForces);
            Rigidbody = null;
        }

        public void InitializeState() { }
    }
#endif

}

