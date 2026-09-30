using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishNet.Example
{
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
        private LocalConnectionState _clientState = LocalConnectionState.Stopped;
        /// <summary>
        /// Current state of server socket.
        /// </summary>
        private LocalConnectionState _serverState = LocalConnectionState.Stopped;
#if !ENABLE_INPUT_SYSTEM
        /// <summary>
        /// EventSystem for the project.
        /// </summary>
        private EventSystem _eventSystem;
#endif
        #endregion

        private void OnGUI()
        {
#if ENABLE_INPUT_SYSTEM
            // Canvas buttons can't receive clicks without an input module, so draw invisible IMGUI buttons over them.
            if (_serverIndicator.gameObject.activeInHierarchy && InvisibleButton(_serverIndicator))
                OnClick_Server();
            if (InvisibleButton(_clientIndicator))
                OnClick_Client();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private readonly Vector3[] _corners = new Vector3[4];

        /// <summary>
        /// Draws an invisible IMGUI button over img. Returns true when clicked.
        /// </summary>
        private bool InvisibleButton(Image img)
        {
            // Overlay canvas, so world corners are screen pixels. IMGUI y is top-down.
            img.rectTransform.GetWorldCorners(_corners);
            Rect r = new(_corners[0].x, Screen.height - _corners[1].y, _corners[2].x - _corners[0].x, _corners[1].y - _corners[0].y);

            // Mimic the canvas Button's color tint, since it gets no pointer events without an input module.
            Button button = img.GetComponentInChildren<Button>();
            if (button != null && Event.current.type == EventType.Repaint)
            {
                bool hover = r.Contains(Event.current.mousePosition);
                ColorBlock colors = button.colors;
                Color tint = !hover ? colors.normalColor : GUIUtility.hotControl != 0 ? colors.pressedColor : colors.highlightedColor;
                button.targetGraphic.CrossFadeColor(tint * colors.colorMultiplier, colors.fadeDuration, true, true);
            }

            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
#endif

        private void Start()
        {
#if !ENABLE_INPUT_SYSTEM
            SetEventSystem();
            BaseInputModule inputModule = FindObjectOfType<BaseInputModule>();
            if (inputModule == null)
                gameObject.AddComponent<StandaloneInputModule>();
#else
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                _serverIndicator.gameObject.SetActive(false);
#endif

            _networkManager = FindObjectOfType<NetworkManager>();
            if (_networkManager == null)
            {
                Debug.LogError("NetworkManager not found, HUD will not function.");
                return;
            }
            else
            {
                UpdateColor(LocalConnectionState.Stopped, ref _serverIndicator);
                UpdateColor(LocalConnectionState.Stopped, ref _clientIndicator);
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
        /// <param name = "state"></param>
        /// <param name = "img"></param>
        private void UpdateColor(LocalConnectionState state, ref Image img)
        {
            Color c;
            if (state == LocalConnectionState.Started)
                c = _startedColor;
            else if (state == LocalConnectionState.Stopped)
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

            if (_serverState != LocalConnectionState.Stopped)
                _networkManager.ServerManager.StopConnection(true);
            else
                _networkManager.ServerManager.StartConnection();

            DeselectButtons();
        }

        public void OnClick_Client()
        {
            if (_networkManager == null)
                return;

            if (_clientState != LocalConnectionState.Stopped)
                _networkManager.ClientManager.StopConnection();
            else
                _networkManager.ClientManager.StartConnection();

            DeselectButtons();
        }

        private void SetEventSystem()
        {
#if !ENABLE_INPUT_SYSTEM
            if (_eventSystem != null)
                return;
            _eventSystem = FindObjectOfType<EventSystem>();
            if (_eventSystem == null)
                _eventSystem = gameObject.AddComponent<EventSystem>();
#endif
        }

        private void DeselectButtons()
        {
#if !ENABLE_INPUT_SYSTEM
            SetEventSystem();
            _eventSystem?.SetSelectedGameObject(null);
#endif
        }
    }
}