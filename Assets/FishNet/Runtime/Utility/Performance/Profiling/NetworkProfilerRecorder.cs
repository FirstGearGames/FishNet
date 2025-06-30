using UnityEngine;
using System.Linq;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;


#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace FishNet.Utility.Performance.Profiling
{
    [DefaultExecutionOrder(int.MaxValue)] // last
    public class NetworkProfilerRecorder : MonoBehaviour
    {
        // singleton because unity only has 1 profiler
        public static NetworkProfilerRecorder Instance { get; private set; }

        public ClientManager Client;
        public ServerManager Server;

        internal static CountRecorder _sentCounter;
        internal static CountRecorder _receivedCounter;
        internal const int FRAME_COUNT = 300; // todo find a way to get real frame count

        public delegate void FrameUpdate(int tick);
        public static event FrameUpdate AfterSample;

        private int _lastProcessedFrame = -1;

        private void Start()
        {
#if UNITY_EDITOR
            _lastProcessedFrame = ProfilerDriver.lastFrameIndex;
#endif

            Debug.Assert(Instance == null);
            Instance = this;
            DontDestroyOnLoad(this);

            // if (Server != null)
            // {
            //     Server.;
            //     Server.Stopped.AddListener(ServerStopped);
            // }
            if (Client == null)
            {
                Client = InstanceFinder.ClientManager;
            }

            if (Client != null)
            {
                Client.OnClientConnectionState += ClientConnectionStateChanged;
            }
        }

        private void OnDestroy()
        {
            //if (_receivedCounter != null)
            //    NetworkDiagnostics.InMessageEvent -= _receivedCounter.OnMessage;
            //if (_sentCounter != null)
            //    NetworkDiagnostics.OutMessageEvent -= _sentCounter.OnMessage;

            Debug.Assert(Instance == this);
            Instance = null;
        }

        private void ServerStarted()
        {
            //if (instance != null)
            //{
            //    logger.LogWarning($"Already started profiler for different Instance:{instance}");
            //    return;
            //}
            //instance = Server;

            //var provider = new NetworkInfoProvider(Server.World);
            //_sentCounter = new CountRecorder(Server, provider, Counters.SentCount, Counters.SentBytes, Counters.SentPerSecond);
            //_receivedCounter = new CountRecorder(Server, provider, Counters.ReceiveCount, Counters.ReceiveBytes, Counters.ReceivePerSecond);
            //NetworkDiagnostics.InMessageEvent += _receivedCounter.OnMessage;
            //NetworkDiagnostics.OutMessageEvent += _sentCounter.OnMessage;
        }

        private void ClientConnectionStateChanged(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    ClientStarted();
                    break;
                case LocalConnectionState.Stopped:
                    ClientStopped();
                    break;
            }
        }

        private void ClientStarted()
        {
            _sentCounter = new CountRecorder(Client, Counters.ReceiveCount, Counters.ReceiveBytes, Counters.ReceivePerSecond);
            _receivedCounter = new CountRecorder(Client, Counters.ReceiveCount, Counters.ReceiveBytes, Counters.ReceivePerSecond);
            Client.OnPacketRead += _receivedCounter.OnMessage;
            //NetworkDiagnostics.OutMessageEvent += _sentCounter.OnMessage;
        }

        private void ServerStopped()
        {
            //if (instance == (object)Server)
            //    instance = null;
        }

        private void ClientStopped(/*ClientStoppedReason _*/)
        {
            _receivedCounter = null;
            _sentCounter = null;
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!ProfilerDriver.enabled)
                return;

            // once a frame, ever frame, no matter what lastFrameIndex is
            SampleCounts();

            // unity sometimes skips a profiler frame, because unity
            // so we have to check if that happens and then sample the missing frame
            while (_lastProcessedFrame < ProfilerDriver.lastFrameIndex)
            {
                _lastProcessedFrame++;

                //Debug.Log($"Sample: [LateUpdate, enabled { ProfilerDriver.enabled}, first {ProfilerDriver.firstFrameIndex}, last {ProfilerDriver.lastFrameIndex}, lastProcessed {lastProcessedFrame}]");

                var lastFrame = _lastProcessedFrame;
                // not sure why frame is offset, but +2 fixes it
                SampleMessages(lastFrame + 2);
            }
#else
            // in player, just use ProfilerCounter (frameCount only used by messages) 
            SampleCounts();
            SampleMessages(0);
#endif
        }

        /// <summary>
        /// call this every frame to sample number of players and objects
        /// </summary>
        private void SampleCounts()
        {
            if (Client != null)
            {
                Counters.AllPlayersCount.Sample(Client.Clients.Count);
                Counters.ObjectCount.Sample(Client.Objects.Spawned.Count);
            } else if (Server != null)
            {
                Counters.AllPlayersCount.Sample(Server.Clients.Count);
                Counters.ObjectCount.Sample(Server.Objects.Spawned.Count);
            }
        }

        /// <summary>
        /// call this when ProfilerDriver shows it is next frame
        /// </summary>
        /// <param name="frame"></param>
        private void SampleMessages(int frame)
        {

            if (_sentCounter != null)
            {
                _sentCounter.EndFrame(frame);
            }

            if (_receivedCounter != null)
            {
                _receivedCounter.EndFrame(frame);
            }
            AfterSample?.Invoke(frame);
        }
    }
}
