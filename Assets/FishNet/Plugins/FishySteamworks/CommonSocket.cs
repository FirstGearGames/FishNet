#if !DISABLESTEAMWORKS
using FishNet.Transporting;
using Steamworks;
using System;
using System.Net;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishySteamworks
{

    public abstract class CommonSocket
    {

        #region Public.
        /// <summary>
        /// Current ConnectionState.
        /// </summary>
        private LocalConnectionStates _connectionState = LocalConnectionStates.Stopped;
        /// <summary>
        /// Returns the current ConnectionState.
        /// </summary>
        /// <returns></returns>
        internal LocalConnectionStates GetConnectionState()
        {
            return _connectionState;
        }
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected void SetLocalConnectionState(LocalConnectionStates connectionState)
        {
            //If state hasn't changed.
            if (connectionState == _connectionState)
                return;

            _connectionState = connectionState;
            Transport.HandleClientConnectionState(
                new ClientConnectionStateArgs(connectionState)
                );
        }
        #endregion

        #region Protected.
        /// <summary>
        /// True if using PeerToPeer.
        /// </summary>
        protected bool PeerToPeer = false;
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        protected Transport Transport = null;
        /// <summary>
        /// Pointers for received messages per connection.
        /// </summary>
        protected IntPtr[] MessagePointers = new IntPtr[MAX_MESSAGES];
        /// <summary>
        /// Buffer used to receive data.
        /// </summary>
        protected byte[] InboundBuffer = null;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum number of messages which can be received per connection.
        /// </summary>
        protected const int MAX_MESSAGES = 256;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal virtual void Initialize(Transport t)
        {
            Transport = t;
            //Get whichever channel has max MTU and resize buffer.
            int maxMTU = Transport.GetMTU(0);
            maxMTU = Math.Max(maxMTU, Transport.GetMTU(1));
            InboundBuffer = new byte[maxMTU];
        }

        /// <summary>
        /// Gets bytes for address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected byte[] GetIPBytes(string address)
        {
            //If address is required then make sure it can be parsed.
            if (!string.IsNullOrEmpty(address))
            {
                if (!IPAddress.TryParse(address, out IPAddress result))
                {
                    Debug.LogError($"Could not parse address {address} to IPAddress.");
                    return null;
                }
                else
                {
                    return result.GetAddressBytes();
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Sends data over the steamConnection.
        /// </summary>
        /// <param name="steamConnection"></param>
        /// <param name="segment"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        protected EResult Send(HSteamNetConnection steamConnection, ArraySegment<byte> segment, byte channelId)
        {
            /* Have to resize array to include channel index
             * if array isn't large enough to fit it. This is because
             * we don't know what channel data comes in on so
             * the channel has to be packed into the data sent.
             * Odds of the array having to resize are extremely low
             * so while this is not ideal, it's still very low risk. */
            if ((segment.Array.Length - 1) <= (segment.Offset + segment.Count))
            {
                byte[] arr = segment.Array;
                Array.Resize(ref arr, arr.Length + 1);
                arr[arr.Length - 1] = channelId;
            }
            //If large enough just increase the segment and set the channel byte.
            else
            {
                segment.Array[segment.Offset + segment.Count] = channelId;
            }
            //Make a new segment so count is right.
            segment = new ArraySegment<byte>(segment.Array, segment.Offset, segment.Count + 1);

            GCHandle pinnedArray = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
            IntPtr pData = pinnedArray.AddrOfPinnedObject() + segment.Offset;

            int sendFlag = (channelId == (byte)Channel.Unreliable) ? Constants.k_nSteamNetworkingSend_Unreliable : Constants.k_nSteamNetworkingSend_Reliable;
            EResult result = SteamNetworkingSockets.SendMessageToConnection(steamConnection, pData, (uint)segment.Count, sendFlag, out long _);
            if (result != EResult.k_EResultOK)
            {
                Debug.LogWarning($"Send issue: {result}");
            }

            pinnedArray.Free();
            return result;
        }

        /// <summary>
        /// Returns a message from the steam network.
        /// </summary>
        /// <param name="ptrs"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected (ArraySegment<byte>, byte) GetMessage(IntPtr ptrs, byte[] buffer)
        {
            SteamNetworkingMessage_t data = Marshal.PtrToStructure<SteamNetworkingMessage_t>(ptrs);

            int packetLength = data.m_cbSize;
            Marshal.Copy(data.m_pData, buffer, 0, packetLength);
            //Channel will be at the end of the packet.
            byte channel = buffer[packetLength - 1];
            //Set segment to length - 1 to exclude channel.
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer, 0, packetLength - 1);

            return (segment, channel);
        }
    }

}
#endif // !DISABLESTEAMWORKS