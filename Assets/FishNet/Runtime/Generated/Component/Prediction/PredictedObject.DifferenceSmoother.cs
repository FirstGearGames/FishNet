using FishNet.Object;

namespace FishNet.Component.Prediction
{
    public partial class PredictedObject : NetworkBehaviour
    {
        private void DifferenceSmoother_Update()
        {
            MoveToTarget();
        }


        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary> 
        private void DifferenceSmoother_TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            //Requires to be owner.
            if (!base.IsOwner)
                return;
            if (_smoothTicks)
                return;

            SetPreviousTransformProperties();
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        private void DifferenceSmoother_TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            //Requires to be owner.
            if (!base.IsOwner)
                return;
            if (!CanSmooth())
                return;

            ResetToTransformPreviousProperties();
            SetTransformMoveRates(_smoothingDuration);
        }

    }


}