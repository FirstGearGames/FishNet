using FishNet.Object;

namespace FishNet.Component.Prediction
{
    public partial class PredictedObject : NetworkBehaviour
    {
        private void OnDisable()
        {
            ChangeSubscriptions(false);
        }


        partial void PartialDifferenceSmoother_Update()
        {
            MoveToTarget();
        }


        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary> 
        partial void PartialDifferenceSmoother_TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            //Requires to be owner.
            if (!base.IsOwner)
                return;
            if (_smoothTicks)
                return;

            _previousPosition = _graphicalObject.position;
            _previousRotation = _graphicalObject.rotation;
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        partial void PartialDifferenceSmoother_TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            //Requires to be owner.
            if (!base.IsOwner)
                return;
            if (!CanSmooth())
                return;

            //Set transform back to where it was before reconcile so there's no visual disturbances.
            _graphicalObject.SetPositionAndRotation(_previousPosition, _previousRotation);
            SetTransformMoveRates(_smoothingDuration);
        }

    }


}