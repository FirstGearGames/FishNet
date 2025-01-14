using FishNet.Component.Prediction;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Demo.Prediction.Rigidbodies
{
    public class Boost : NetworkBehaviour
    {
        [SerializeField]
        private float _rotateRate = 90f;

        private void Awake()
        {
            NetworkTrigger networkTrigger = GetComponent<NetworkTrigger>();
            /* No need to unsubscribe this networkTrigger is on this object.
             * The subscription will die with the object. */
            networkTrigger.OnEnter += NetworkTrigger_OnEnter;
        }

        private void Update()
        {
            transform.Rotate(new Vector3(0f, 1f, 0f) * (_rotateRate * Time.deltaTime));
        }

        private void NetworkTrigger_OnEnter(Collider c)
        {
            if (!c.transform.root.TryGetComponent(out RigidbodyPrediction rbp)) return;

            /* When the vehicle enters this object call set boosted.
             * This trigger will invoke if the client enters it after a reconcile as well.
             * Because of this, it's not unusual to see enter/exit called many times over a second
             * due to the vehicle reconciling and running through the trigger again. */
            rbp.SetBoosted();
        }
    }
}