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
        //public static void WritePredictionRigidbody(this Writer w, PredictionRigidbody pr)
        //{
        //    Debug.Log("Writing ");
        //    w.WriteList<PredictionRigidbody.ForceData>(pr.PendingForces);
        //}

        //public static PredictionRigidbody ReadPredictionRigidbody(this Reader r)
        //{
        //    List<PredictionRigidbody.ForceData> lst = CollectionCaches<PredictionRigidbody.ForceData>.RetrieveList();
        //    r.ReadList(ref lst);
        //    PredictionRigidbody pr = ResettableObjectCaches<PredictionRigidbody>.Retrieve();
        //    pr.PendingForces = lst;
        //    Debug.Log($"{lst == null}, {pr.PendingForces == null}");
        //    return pr;
        //}
    }

    public class PredictionRigidbody : IResettable
    {
        #region Types.
        internal struct ForceData
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

        #region Internal.
        /// <summary>
        /// Forces waiting to be applied.
        /// </summary>
        [ExcludeSerialization]
        internal List<ForceData> PendingForces;
        #endregion

        ~PredictionRigidbody()
        {
            if (PendingForces != null)
                CollectionCaches<ForceData>.StoreAndDefault(ref PendingForces);
            Rigidbody = null;
        }

        /// <summary>
        /// Rigidbody which force is applied.
        /// </summary>
        /// <param name="rb"></param>
        public void Initialize(Rigidbody rb)
        {
            Debug.LogError($"This utility is a work in progress. Please do not use it at this time.");
            Rigidbody = rb;
            if (PendingForces == null)
                PendingForces = CollectionCaches<ForceData>.RetrieveList();
            else
                PendingForces.Clear();
        }

        /// <summary>
        /// Adds Velocity force to the Rigidbody.
        /// </summary>
        public void AddForce(Vector3 force, ForceMode mode =  ForceMode.Force)
        {
            PendingForces.Add(new ForceData(force, mode, true));
        }

        public void AddAngularForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            PendingForces.Add(new ForceData(force, mode, false));
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
            foreach (ForceData item in PendingForces)
            {
                if (item.IsVelocity)
                    Rigidbody.AddForce(item.Force, item.Mode);
                else
                    Rigidbody.AddTorque(item.Force, item.Mode);
            }
            PendingForces.Clear();
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
            PendingForces.Clear();
        }

        /// <summary>
        /// Reconciles to a state.
        /// </summary>
        public void Reconcile(PredictionRigidbody pr)
        {
            PendingForces.Clear();
            foreach (ForceData item in pr.PendingForces)
                PendingForces.Add(new ForceData(item));

            ResettableObjectCaches<PredictionRigidbody>.Store(pr);
        }

        /// <summary>
        /// Removes forces from pendingForces.
        /// </summary>
        /// <param name="velocity">True to remove if velocity.</param>
        private void RemoveForces(bool velocity)
        {
            if (PendingForces.Count > 0)
            {
                List<ForceData> newDatas = CollectionCaches<ForceData>.RetrieveList();
                foreach (ForceData item in PendingForces)
                {
                    if (item.IsVelocity != velocity)
                        newDatas.Add(item);
                }
                //Add back to _pendingForces if changed.
                if (newDatas.Count != PendingForces.Count)
                {
                    PendingForces.Clear();
                    foreach (ForceData item in newDatas)
                        PendingForces.Add(item);
                }
                CollectionCaches<ForceData>.Store(newDatas);
            }
        }

        public void ResetState()
        {
            CollectionCaches<ForceData>.StoreAndDefault(ref PendingForces);
            Rigidbody = null;
        }

        public void InitializeState() { }
    }
#endif

}

