using FishNet.CodeGenerating;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.PredictionV2
{

    public class SpringBoard : NetworkBehaviour
    {
#if PREDICTION_V2

        public float Force = 20f;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out RigidbodyPredictionV2 rp2))
            {
                if (PredictionManager.IsReconciling)
                    Debug.Log($"Frame {Time.frameCount}. Replay LocalTick {PredictionManager.ClientReplayTick}. Last  MdTick {rp2.LastMdTick}. Velocity {rp2.Rigidbody.velocity.magnitude}");
                else
                    Debug.Log($"Frame {Time.frameCount}. Current LocalTick {TimeManager.LocalTick}. MdTick {rp2.LastMdTick}. Velocity {rp2.Rigidbody.velocity.magnitude}");
                //rp2.Rigidbody.AddForce(Vector3.left * Force, ForceMode.Impulse);
                //rp2.Rigidbody.AddImpulseVelocity(Vector3.left * Force);
                rp2.PRB.AddForce(Vector3.left * Force, ForceMode.Impulse, true);
            }
            else
            {
                Debug.LogError($"SOME OTHER OBJECT HIT");
            }
        }

#endif
    }

}

public class PredictionRigidbody
{
    public Rigidbody Rigidbody { get; private set; }

    private Vector3 _impulse;
    private Vector3 _force;
    public PredictionRigidbody(Rigidbody rb)
    {
        Rigidbody = rb;
    }


    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force, bool afterSimulation = false)
    {
        if (afterSimulation)
        {
            _impulse += force;
            return;
        }

        if (_impulse != Vector3.zero)
        {
            Rigidbody.AddForce(_impulse, ForceMode.Impulse);
            _impulse = Vector3.zero;
        }

        Rigidbody.AddForce(force, mode);
    }


}