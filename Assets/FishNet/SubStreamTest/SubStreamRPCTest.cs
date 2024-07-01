using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Connection;
using UnityEditor.MemoryProfiler;
using UnityEngine.UIElements;

public class SubStreamRPCTest : NetworkBehaviour
{
    public override void OnSpawnServer(NetworkConnection conn) {
        base.OnSpawnServer(conn);

        TestWritingToSubStream(conn);
    }

    public override void OnStartClient() {
        base.OnStartClient();

        using (SubStream subStream = SubStream.StartWriting(NetworkManager, out PooledWriter writer)) {

            writer.Write("Hello world from client!");
            writer.Write("I am calling ServerRPC!");
            writer.Write("Stuff!");

            TestServerRPC(subStream);

            //ShouldFailTestServerRPC(subStream);
        }
    }

    public void TestWritingToSubStream(NetworkConnection conn) {

        #region TargetRPC Test
        SubStream streamA = SubStream.StartWriting(this.NetworkManager, out PooledWriter writerA);
        SubStream streamB = SubStream.StartWriting(this.NetworkManager, out PooledWriter writerB);

        writerA.Write("StreamA");
        writerB.Write("StreamB");

        TestTargetRPC(conn, streamA, streamB);
        TestTargetRPC(conn, default, streamB);
        TestTargetRPC(conn, streamA, default);
        TestTargetRPC(conn, default, default);

        streamA.Dispose();
        streamB.Dispose();
        #endregion

        #region Struct Test
        TestStruct testStr = new TestStruct() {
            Pos = Vector3.one,
            Vel = Vector3.one
        };

        using (SubStream structStream = SubStream.StartWriting(this.NetworkManager, out PooledWriter writerStruct)) {

            writerStruct.Write("StructStream");

            testStr.Stream = structStream;

            TestStructObserverRPC(testStr);
        }
        #endregion

        #region Mixed Parameters Test

        using (SubStream mixedStream = SubStream.StartWriting(NetworkManager, out PooledWriter writer)) {

            writer.Write(10);
            writer.Write("String inside stream sent over RPC!");
            writer.Write(10);
            writer.Write(Vector3.one);

            MixedParametersObserverRPC(Vector3.one, mixedStream, "SimpleString", 3.14f);
        }

        #endregion
    }


    [TargetRpc]
    public void TestTargetRPC(NetworkConnection conn, SubStream streamA, SubStream streamB) {

        Debug.Log("[TestTargetRPC]");

        if(streamA.StartReading(out Reader readerA)) {

            if(readerA.ReadString() != "StreamA") {
                Debug.LogError("StreamA string does not match!");
            }

            streamA.Dispose();
        }

        if(streamB.StartReading(out Reader readerB)) {

            if(readerB.ReadString() != "StreamB") {
                Debug.LogError("StreamB string does not match!");
            }

            streamB.Dispose();
        }        
    }

    public struct TestStruct {
        public Vector3 Pos;
        public SubStream Stream;
        public Vector3 Vel;
    }
    
    [ObserversRpc]
    public void TestStructObserverRPC(TestStruct testStruct) {

        Debug.Log("[TestObserverRPC]");

        if(testStruct.Pos != Vector3.one) {
            Debug.LogError("Position does not match!");
        }

        if(testStruct.Vel != Vector3.one) {
            Debug.LogError("Velocity does not match!");
        }

        if (testStruct.Stream.StartReading(out Reader reader)) {

            if(reader.ReadString() != "StructStream") {
                Debug.LogError("String does not match!");
            }
        }
    }

    [ObserversRpc]
    public void MixedParametersObserverRPC(Vector3 pos, SubStream dataStream, string text, float number) {

        Debug.Log("[MixedParametersObserverRPC]");

        if(pos != Vector3.one) {
            Debug.LogError("pos does not match!");
        }

        if(text != "SimpleString") {
            Debug.LogError("text does not match!");
        }

        if(number != 3.14f) {
            Debug.LogError("number does not match!");
        }

        if(dataStream.StartReading(out Reader reader)) {

            if(reader.ReadInt32() != 10) {
                Debug.LogError("Int does not match!");
            }

            if(reader.ReadString() != "String inside stream sent over RPC!") {
                Debug.LogError("String does not match!");
            }

            if(reader.ReadInt32() != 10) {
                Debug.LogError("Int does not match!");
            }

            if(reader.ReadVector3() != Vector3.one) {
                Debug.LogError("Vector3 does not match!");
            }

            dataStream.Dispose();
        }

    }
    
    void FixedUpdate()
    {       
        using(SubStream stream = SubStream.StartWriting(NetworkManager, out PooledWriter writer)) {

            writer.Write("Hello world");

            int length = UnityEngine.Random.Range(5, 15);

            int hash = length.GetHashCode();

            writer.WriteInt16((short)length);
            for(int i = 0; i < length; i++) {

                var randomInt = UnityEngine.Random.Range(-100, 100);
                writer.WriteInt32(randomInt);
                hash ^= randomInt.GetHashCode();
            }

            writer.WriteInt32(hash);

            FixedUpdateObserverRPC(Time.timeSinceLevelLoad, stream);
        }
    }

    [ObserversRpc]
    void FixedUpdateObserverRPC(float time, SubStream data) {

        //Debug.Log("[FixedUpdateObserverRPC]");

        if(data.StartReading(out Reader reader)) {

            var str = reader.ReadString();

            int length = reader.ReadInt16();
            
            int calculatedHash = length.GetHashCode();

            for(int i = 0; i < length; i++) {
                var randomInt = reader.ReadInt32();
                calculatedHash ^= randomInt.GetHashCode();
            }

            var sentHash = reader.ReadInt32();

            if(sentHash != calculatedHash) {
                Debug.LogError("Hashes do not match!");
            }

            data.Dispose();
        }
    }


    [ServerRpc(RequireOwnership = false)]
    void TestServerRPC(SubStream stream, NetworkConnection conn = null) {

        Debug.Log("[TestServerRPC]");

        if(stream.StartReading(out Reader reader)) {


            if(reader.ReadString() != "Hello world from client!") {
                Debug.LogError("String does not match!");
            }

            if(reader.ReadString() != "I am calling ServerRPC!") {
                Debug.LogError("String does not match!");
            }

            if(reader.ReadString() != "Stuff!") {
                Debug.LogError("String does not match!");
            }

            stream.Dispose();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void ShouldFailTestServerRPC(SubStream stream, NetworkConnection conn = null) {

        Debug.Log("[ShouldFailTestServerRPC]");

        if(stream.StartReading(out Reader reader)) {

            reader.ReadString();
            reader.ReadString();
            reader.ReadString();

            //fails here, note that not all Reader methods have reading protection
            reader.ReadString();

            stream.Dispose();
        }
    }

}
