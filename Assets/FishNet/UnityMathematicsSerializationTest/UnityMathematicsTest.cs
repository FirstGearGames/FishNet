using FishNet.Connection;
using FishNet.Object;

using UnityEngine;
using Unity.Mathematics;
using FishNet.Serializing;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;


public class UnityMathematicsTest : NetworkBehaviour {

    private void Awake() {
        Debug.Log($"Size of test struct: {Marshal.SizeOf<MathTestStruct>()} in bytes");
    }

    const int sendsCound = 100;

    

    private void FixedUpdate() {
         
        if (IsHostInitialized) {

            uint seed = this.TimeManager.Tick;

            var random = new Unity.Mathematics.Random(seed);

            var writer = WriterPool.Retrieve(this.NetworkManager, 2050 * sendsCound + 8);

            for (int i = 0; i < sendsCound; i++) {

                uint innerSeed = random.NextUInt();

                var testData = MathTestStruct.GenerateFromSeed(innerSeed);

                writer.Write<MathTestStruct>(testData);
            }

            TestSerializationRPC(seed, writer.GetArraySegment());

            WriterPool.StoreLength(writer);
            
        }   
    }

    // never to be called, just to activate serializer autogen for the struct (no way to force auto gen serializer yet)
    [ServerRpc()]
    private void NeverToBeCalledRPC(MathTestStruct x)
    {

    }
    [ServerRpc(RequireOwnership = false, DataLength = 2040 * sendsCound + 4)]
    private void TestSerializationRPC(uint seed, ArraySegment<byte> data, NetworkConnection conn = null) {

        var random = new Unity.Mathematics.Random(seed);

        var reader = ReaderPool.Retrieve(data, this.NetworkManager);

        for (int i = 0; i < sendsCound; i++) {

            uint innerSeed = random.NextUInt();

            var receivedData = reader.Read<MathTestStruct>();
                
            var testData = MathTestStruct.GenerateFromSeed(innerSeed);

            if (!IsEqual(receivedData, testData)) {
                UnityEngine.Debug.LogError("Data does not match! Serialization faulty");
            }
            else {
                UnityEngine.Debug.Log("All fine!");
            } 
        }

        ReaderPool.Store(reader);

    }


    // just compare struct by value
    private bool IsEqual(MathTestStruct a, MathTestStruct b) {

        return EqualityComparer<MathTestStruct>.Default.Equals(a, b);
    }
}
