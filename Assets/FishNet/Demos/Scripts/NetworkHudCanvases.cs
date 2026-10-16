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
        /// <summary>
        /// World corners of the image being drawn over, reused each call.
        /// </summary>
        private readonly Vector3[] _corners = new Vector3[4];

        /// <summary>
        /// Draws an invisible IMGUI button over img. Returns true when clicked.
        /// </summary>
        private bool InvisibleButton(Image img)
        {
            img.rectTransform.GetWorldCorners(_corners);

            // An overlay canvas has no camera; Screen Space Camera and World Space canvases render through one.
            Canvas canvas = img.canvas.rootCanvas;
            Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            // Bounding box of the corners in screen space, so any canvas mode, scale or rotation gets the right area.
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < _corners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, _corners[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            // IMGUI's y axis runs top down.
            Rect r = new(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);

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
            #if UNITY_2022_2_OR_NEWER
            BaseInputModule inputModule = FindAnyObjectByType<BaseInputModule>();
            #else
            BaseInputModule inputModule = FindObjectOfType<BaseInputModule>();
            #endif
            if (inputModule == null)
                gameObject.AddComponent<StandaloneInputModule>();
#else
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                _serverIndicator.gameObject.SetActive(false);
#endif

            #if UNITY_2022_2_OR_NEWER
            _networkManager = FindAnyObjectByType<NetworkManager>();
            #else
            _networkManager = FindObjectOfType<NetworkManager>();
            #endif
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
            #if UNITY_2022_2_OR_NEWER
            _eventSystem = FindAnyObjectByType<EventSystem>();
            #else
            _eventSystem = FindObjectOfType<EventSystem>();
            #endif
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