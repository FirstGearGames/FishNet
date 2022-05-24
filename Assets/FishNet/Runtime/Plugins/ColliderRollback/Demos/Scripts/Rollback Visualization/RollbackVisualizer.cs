using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.ColliderRollback.Demo
{

    /// <summary>
    /// DEMO. CODE IS NOT OPTIMIZED.
    /// Shows where an object was when client hit it, and where it was after server rolled it back.
    /// </summary>

    public class RollbackVisualizer : NetworkBehaviour
    {
        [SerializeField]
        private GameObject _originalPrefab;
        [SerializeField]
        private GameObject _rollbackPrefab;
        [SerializeField]
        private TextCanvas _textCanvasPrefab;

        /// <summary>
        /// Shows difference between where object was when client shot it, and where it was after rollback.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="rolledBack"></param>
        [Server]
        public void ShowDifference(NetworkObject clientObject, Vector3 original, Vector3 rolledBack)
        {
            TargetShowDifference(clientObject.Owner, original, rolledBack);
        }


        [TargetRpc]
        private void TargetShowDifference(NetworkConnection conn, Vector3 original, Vector3 rollback)
        {
            Instantiate(_originalPrefab, original, transform.rotation);
            Instantiate(_rollbackPrefab, rollback, transform.rotation);

            float difference = Vector3.Distance(original, rollback);
            TextCanvas tc = Instantiate(_textCanvasPrefab);
            string text = $"Accuracy is within {difference} units.";
            tc.SetText(text);
            Debug.Log(text);
        }

    }

}