#if UNITY_EDITOR || !UNITY_SERVER
using System;
using FishNet.Component.Utility;
using FishNet.Connection;
using FishNet.Managing.Statistic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.UI;

namespace FishNet.Demo.Benchmarks.NetworkTransforms
{
    public class PrefabSpawner : NetworkBehaviour
    {
        [Header("General")]
        [SerializeField]
        private NetworkObject _prefab;

        [Header("Spawning")]
        [SerializeField]
        private int _count = 500;

        [Header("Display")]
        [SerializeField]
        private Text _displayText;

        // [SerializeField]
        // private float _xyRange = 15f;
        // [SerializeField]
        // private float _zRange = 100f;

        private float _resetBandwidthTime = float.NegativeInfinity;

        public override void OnStartServer()
        {
            if (_prefab == null)
            {
                Debug.LogError($"Prefab is null.");
                return;
            }

            NetworkObject prefab = _prefab;
            Vector3 currentPosition = transform.position;

            for (int i = 0; i < _count; i++)
            {
                NetworkObject nob = Instantiate(prefab, currentPosition, Quaternion.identity);
                base.Spawn(nob);
            }
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            //Reset bandwidth half a second after spawning in objects for a client.
            _resetBandwidthTime = Time.time + 1f;
        }

        private void Update()
        {
            if (_displayText == null)
                return;
            if (!base.IsServerInitialized)
                return;

            if (_resetBandwidthTime != float.NegativeInfinity && Time.time >= _resetBandwidthTime)
            {
                _resetBandwidthTime = float.NegativeInfinity;
                BandwidthDisplay bd = GameObject.FindObjectOfType<BandwidthDisplay>();
                if (bd != null)
                {
                    bd.ResetAverages();
                    Debug.Log($"Resetting bandwidth averages.");
                }
            }

            uint updateFrequency = (uint)Mathf.FloorToInt((float)base.TimeManager.TickRate / 4f);
            if (updateFrequency < 1)
                updateFrequency = 1;

            if (base.TimeManager.LocalTick % updateFrequency == 0)
            {
                _displayText.text = "Spawned: " + _count;
                _displayText.text += Environment.NewLine + "Tick Rate: " + base.TimeManager.TickRate;

                BandwidthDisplay bd = base.NetworkManager.gameObject.GetComponent<BandwidthDisplay>();
                ulong serverOutAverage = bd.ServerAverages.GetAverage(inAverage: false);

                float perTransformAverage = (float)serverOutAverage / _count;
                _displayText.text += Environment.NewLine + "Average Per Transform: " + $"{NetworkTraficStatistics.FormatBytesToLargest(perTransformAverage)}/s";
            }
        }
    }
}
#endif