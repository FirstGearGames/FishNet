using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Timing;
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
        /// Average accuracy over the past 30 shots.
        /// </summary>
        private List<float> _accuracyAverage = new List<float>();

        private void OnDisable()
        {
            _accuracyAverage.Clear();
        }

        /// <summary>
        /// Shows difference between where object was when client shot it, and where it was after rollback.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="rolledBack"></param>
        [Server]
        public void ShowDifference(NetworkObject clientObject, Vector3 original, Vector3 rolledBack)
        {
            //Only send to client if not host.
            if (!base.IsHost)
            {
                float difference = Vector3.Distance(original, rolledBack);
                PrintAverage(false, difference, base.NetworkManager);
                TargetShowDifference(clientObject.Owner, original, rolledBack);
            }
        }


        [TargetRpc]
        private void TargetShowDifference(NetworkConnection conn, Vector3 original, Vector3 rollback)
        {
            Instantiate(_originalPrefab, original, transform.rotation);
            Instantiate(_rollbackPrefab, rollback, transform.rotation);

            float difference = Vector3.Distance(original, rollback);
            string accuracyText = PrintAverage(true, difference, base.NetworkManager);
            TextCanvas tc = Instantiate(_textCanvasPrefab);
            tc.SetText(accuracyText);
        }

        /// <summary>
        /// Prints an average accuracy, returning what was printed.
        /// </summary>
        /// <param name="fromServer">True if difference is received from the server.</param>
        private string PrintAverage(bool fromServer, float difference, NetworkManager nm)
        {
            //If clientHost...
            if (nm.IsHost)
            {
                string result = $"Accuracy will not show properly when as clientHost.{Environment.NewLine}Use a separate client and server for testing.";
                Debug.Log(result);
                return result;
            }
            else
            {
                _accuracyAverage.Add(difference);
                if (_accuracyAverage.Count > 20)
                    _accuracyAverage.RemoveAt(0);

                string currentHit = $"Accuracy is within {difference.ToString("0.0000")} units.";
                string allHit = $"{_accuracyAverage.Count} hit average is {(_accuracyAverage.Sum() / _accuracyAverage.Count).ToString("0.0000")}.";
                string result = $"{currentHit} {allHit}";
                Debug.Log(result);
                return result;
            }
        }
    }

}