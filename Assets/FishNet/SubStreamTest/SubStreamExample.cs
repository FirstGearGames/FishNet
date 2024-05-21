using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Connection;
using UnityEditor.MemoryProfiler;
using UnityEngine.UIElements;

public class SubStreamExample : NetworkBehaviour
{
    // this method calls RPC
    public void SendData()
    {
        var stream = SubStream.StartWriting(NetworkManager, out PooledWriter paramWriter);
        
        // write anything to the stream
        paramWriter.Write("Hello fishes!");
        paramWriter.WriteInt32(128);

        TestObserverRPC(1, stream, Vector3.zero);
        
        // must always be disposed after sending RPC
        stream.Dispose();
    }

    [ObserversRpc]
    void TestObserverRPC(int randomParam, SubStream stream, Vector3 randomVec) {

        if(stream.StartReading(out Reader reader)) {
            // read the data from the stream in same order
            var text = reader.ReadString();
            var number = reader.ReadInt32();

            // must always be disposed after reading
            stream.Dispose();
        }
    }

    struct PathfindingData
    {
        public Vector3 StartPosition;
        public SubStream PathDeltasStream;
    }
}
