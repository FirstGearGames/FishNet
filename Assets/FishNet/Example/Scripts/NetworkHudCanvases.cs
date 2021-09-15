using FishNet.Managing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NetworkHudCanvases : MonoBehaviour
{
    private NetworkManager _networkManager;
    public GameObject ServerButton;
    public GameObject ClientButton;

    public bool AutoStart = true;

    private void Start()
    {
        EventSystem systems = FindObjectOfType<EventSystem>();
        if (systems == null)
            gameObject.AddComponent<EventSystem>();
        StandaloneInputModule inputModule = FindObjectOfType<StandaloneInputModule>();
        if (inputModule == null)
            gameObject.AddComponent<StandaloneInputModule>();

        _networkManager = FindObjectOfType<NetworkManager>();
        if (AutoStart)
        {
            OnClick_Server();
            if (!Application.isBatchMode)
                OnClick_Client();
        }
    }
    private void Update()
    {
        Text t;

        t = ServerButton.GetComponentInChildren<Text>();
        if (_networkManager.ServerManager.Started)
            t.text = "Stop Server";
        else
            t.text = "Start Server";

        t = ClientButton.GetComponentInChildren<Text>();
        if (_networkManager.ClientManager.Started)
            t.text = "Stop Client";
        else
            t.text = "Start Client";
    }

    public void OnClick_Server()
    {
        if (_networkManager.IsServer)
        {
            //Stop client as well.
            if (_networkManager.IsClient)
                _networkManager.TransportManager.Transport.StopConnection(false);
            _networkManager.TransportManager.Transport.StopConnection(true);
        }
        else
        {
            _networkManager.TransportManager.Transport.StartConnection(true);
        }
    }


    public void OnClick_Client()
    {
        if (_networkManager.IsClient)
            _networkManager.TransportManager.Transport.StopConnection(false);
        else
            _networkManager.TransportManager.Transport.StartConnection(false);
    }
}
