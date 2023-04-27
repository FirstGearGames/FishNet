using FishNet.Component.Prediction;
using FishNet.Managing;
using FishNet.Managing.Timing;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object
{
#if PREDICTION_V2
    public partial class NetworkObject : MonoBehaviour
    {
        /// <summary>
        /// NetworkBehaviours which use prediction.
        /// </summary>
        private List<NetworkBehaviour> _predictionBehaviours = new List<NetworkBehaviour>();

        private void Prediction_Preinitialize(NetworkManager manager, bool asServer)
        {
            //Server doesn't need the prereconcile callback.
            if (asServer)
                return;

            if (_predictionBehaviours.Count > 0)
            {
                manager.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
                manager.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
                manager.PredictionManager.OnReplicateReplay += PredictionManager_OnReplicateReplay;
            }
        }

        private void Prediction_Deinitialize(bool asServer)
        {
            //Server doesn't need the prereconcile callback.
            if (asServer)
                return;

            if (_predictionBehaviours.Count > 0 && NetworkManager != null)
            {
                NetworkManager.PredictionManager.OnPreReconcile -= PredictionManager_OnPreReconcile;
                NetworkManager.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
                NetworkManager.PredictionManager.OnReplicateReplay -= PredictionManager_OnReplicateReplay;
            }
        }

        private void PredictionManager_OnPreReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {            
            uint tick = (IsOwner) ? clientReconcileTick : serverReconcileTick;

            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Reconcile_Client_Start();
        }

        private void PredictionManager_OnPostReconcile(uint clientTick, uint serverTick)
        {
            //Rigidbodies may not be paused but calling unpause won't hurt anything.
            UnpauseRigidbodies();
        }


        private void PredictionManager_OnReplicateReplay(uint clientTick, uint serverTick)
        {
            uint replayTick = (IsOwner) ? clientTick : serverTick;
            //if (LastReplicateTick < replayTick)
            //{
            //    PauseRigidbodies();
            //    return;
            //}

            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Replicate_Replay_Start(replayTick);
        }

        /// <summary>
        /// Registers a NetworkBehaviour that uses prediction with the NetworkObject.
        /// This method should only be called once throughout the entire lifetime of this object.
        /// </summary>
        internal void RegisterPredictionBehaviourOnce(NetworkBehaviour nb)
        {
            _predictionBehaviours.Add(nb);
        }

        /// <summary>
        /// Pauses rigidbodies from simulating.
        /// </summary>
        public void PauseRigidbodies()
        {
            if (!_pauserInitialized)
            {
                _pauser.UpdateRigidbodies(transform, RigidbodyType.Rigidbody, false, null);
                _pauserInitialized = true;
            }
            if (!_pauser.Paused)
            {
                if (IsOwner)
                    Debug.Log($"Paused rigidbodies. Owner {IsOwner}");
            }
            _pauser.Pause();
        }

        private bool _pauserInitialized;
        private RigidbodyPauser _pauser = new RigidbodyPauser();

        /// <summary>
        /// Unpauses rigidbodies allowing them to simulate.
        /// </summary>
        public void UnpauseRigidbodies()
        {
            _pauser.Unpause();
        }

        /// <summary>
        /// Last tick this object replicated.
        /// </summary>
        internal uint LastReplicateTick;
    }
#endif
}

