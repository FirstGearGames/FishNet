using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NetworkHudCanvases : MonoBehaviour
{
    #region Types.
    /// <summary>
    /// Ways the HUD will automatically start a connection.
    /// </summary>
    private enum AutoStartType
    {
        Disabled,
        Host,
        Server,
        Client
    }
    #endregion

    #region Serialized.
    /// <summary>
    /// What connections to automatically start on play.
    /// </summary>
    [Tooltip("What connections to automatically start on play.")]
    [SerializeField]
    private AutoStartType _autoStartType = AutoStartType.Disabled;
    /// <summary>
    /// Color when socket is stopped.
    /// </summary>
    [Tooltip("Color when socket is stopped.")]
    [SerializeField]
    private Color _stoppedColor;
    /// <summary>
    /// Color when socket is changing.
    /// </summary>
    [Tooltip("Color when socket is changing.")]
    [SerializeField]
    private Color _changingColor;
    /// <summary>
    /// Color when socket is started.
    /// </summary>
    [Tooltip("Color when socket is started.")]
    [SerializeField]
    private Color _startedColor;
    [Header("Indicators")]
    /// <summary>
    /// Indicator for server state.
    /// </summary>
    [Tooltip("Indicator for server state.")]
    [SerializeField]
    private Image _serverIndicator;
    /// <summary>
    /// Indicator for client state.
    /// </summary>
    [Tooltip("Indicator for client state.")]
    [SerializeField]
    private Image _clientIndicator;
    #endregion

    #region Private.
    /// <summary>
    /// Found NetworkManager.
    /// </summary>
    private NetworkManager _networkManager;
    /// <summary>
    /// Current state of client socket.
    /// </summary>
    private LocalConnectionStates _clientState = LocalConnectionStates.Stopped;
    /// <summary>
    /// Current state of server socket.
    /// </summary>
    private LocalConnectionStates _serverState = LocalConnectionStates.Stopped;
    #endregion

    void OnGUI()
    {
#if ENABLE_INPUT_SYSTEM        
        string GetNextStateText(LocalConnectionStates state)
        {
            if (state == LocalConnectionStates.Stopped)
                return "Start";
            else if (state == LocalConnectionStates.Starting)
                return "Starting";
            else if (state == LocalConnectionStates.Stopping)
                return "Stopping";
            else if (state == LocalConnectionStates.Started)
                return "Stop";
            else
                return "Invalid";
        }

        GUILayout.BeginArea(new Rect(16, 16, 256, 9000));
        Vector2 defaultResolution = new Vector2(1920f, 1080f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / defaultResolution.x, Screen.height / defaultResolution.y, 1));

        GUIStyle style = GUI.skin.GetStyle("button");
        int originalFontSize = style.fontSize;

        Vector2 buttonSize = new Vector2(256f, 64f);
        style.fontSize = 28;
        //Server button.
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            if (GUILayout.Button($"{GetNextStateText(_serverState)} Server", GUILayout.Width(buttonSize.x), GUILayout.Height(buttonSize.y)))
                OnClick_Server();
            GUILayout.Space(10f);
        }

        //Client button.
        if (GUILayout.Button($"{GetNextStateText(_clientState)} Client", GUILayout.Width(buttonSize.x), GUILayout.Height(buttonSize.y)))
            OnClick_Client();

        style.fontSize = originalFontSize;

        GUILayout.EndArea();
#endif
    }

    private void Start()
    {
#if !ENABLE_INPUT_SYSTEM
        EventSystem systems = FindObjectOfType<EventSystem>();
        if (systems == null)
            gameObject.AddComponent<EventSystem>();
        BaseInputModule inputModule = FindObjectOfType<BaseInputModule>();
        if (inputModule == null)
            gameObject.AddComponent<StandaloneInputModule>();
#else
        _serverIndicator.transform.parent.gameObject.SetActive(false);
        _clientIndicator.transform.parent.gameObject.SetActive(false);
#endif

        _networkManager = FindObjectOfType<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("NetworkManager not found, HUD will not function.");
            return;
        }
        else
        {
            UpdateColor(LocalConnectionStates.Stopped, ref _serverIndicator);
            UpdateColor(LocalConnectionStates.Stopped, ref _clientIndicator);
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        }

        if (_autoStartType == AutoStartType.Host || _autoStartType == AutoStartType.Server)
            OnClick_Server();
        if (!Application.isBatchMode && (_autoStartType == AutoStartType.Host || _autoStartType == AutoStartType.Client))
            OnClick_Client();
    }


    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
        _networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
    }

    /// <summary>
    /// Updates img color baased on state.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="img"></param>
    private void UpdateColor(LocalConnectionStates state, ref Image img)
    {
        Color c;
        if (state == LocalConnectionStates.Started)
            c = _startedColor;
        else if (state == LocalConnectionStates.Stopped)
            c = _stoppedColor;
        else
            c = _changingColor;

        img.color = c;
    }


    private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
    {
        _clientState = obj.ConnectionState;
        UpdateColor(obj.ConnectionState, ref _clientIndicator);
    }


    private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
    {
        _serverState = obj.ConnectionState;
        UpdateColor(obj.ConnectionState, ref _serverIndicator);
    }


    public void OnClick_Server()
    {
        if (_networkManager == null)
            return;

        if (_serverState != LocalConnectionStates.Stopped)
            _networkManager.ServerManager.StopConnection(true);
        else
            _networkManager.ServerManager.StartConnection();
    }


    public void OnClick_Client()
    {
        if (_networkManager == null)
            return;

        if (_clientState != LocalConnectionStates.Stopped)
            _networkManager.ClientManager.StopConnection();
        else
            _networkManager.ClientManager.StartConnection();
    }
}
