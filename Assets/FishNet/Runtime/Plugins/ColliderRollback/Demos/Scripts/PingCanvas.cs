using FishNet.Managing;
using UnityEngine;
using UnityEngine.UI;

namespace FishNet.Component.ColliderRollback.Demo
{

    public class PingCanvas : MonoBehaviour
    {
        [SerializeField]
        private Text _text;

        private void Update()
        {
            //Show ping occasionally.
            if (Time.frameCount % 50 != 0)
                return;

            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm != null && nm.IsClient)
                _text.text = $"Ping: {nm.TimeManager.RoundTripTime}";
        }

    }

}