using FishNet.Connection;
using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishNet.Example.ColliderRollbacks
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
        /// <param name = "original"></param>
        /// <param name = "rolledBack"></param>
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
            if (difference <= 0.00001f)
                difference = 0f;

            _differences.Add(difference);
            if (_differences.Count > 20)
                _differences.RemoveAt(0);
            float averageDifference = _differences.Sum() / _differences.Count;

            string accuracyText = IsServerStarted ? $"Accuracy will not show properly when as clientHost.{Environment.NewLine}Use a separate client and server for testing." : $"Difference {difference.ToString("0.000")}m. Average difference {averageDifference.ToString("0.000")}m.";

            TextCanvas tc = Instantiate(_textCanvasPrefab);
            tc.SetText(accuracyText);
            Debug.Log(accuracyText);
        }

        private List<float> _differences = new();
    }
}