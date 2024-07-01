using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Connection;
using UnityEditor.MemoryProfiler;
using UnityEngine.UIElements;
using FishNet.Transporting;
using FishNet.Broadcast;
using static UnityEngine.Analytics.IAnalytic;
using UnityEngine.Analytics;

public class SubStreamBroadcastTest : NetworkBehaviour
{
    private struct BroadcastStruct : IBroadcast
    {
        public SubStream Stream;
    }

    private struct MegaBroadcastStruct : IBroadcast
    {
        public int Integer;
        public float FP;
        public string Text;

        public SubStream StreamA;
        public SubStream StreamB;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    private void FixedUpdate()
    {
        if (IsServerInitialized)
        {
            SendBroadcastStruct();
            SendMegaBroadcastStruct();
        }
    
    }

    private void SendBroadcastStruct()
    {
        SubStream stream = SubStream.StartWriting(this.NetworkManager, out PooledWriter writer);

        WriteRandomDataToBuffer(writer);

        ServerManager.Broadcast<BroadcastStruct>(
            new BroadcastStruct() { Stream = stream }
        );

        stream.Dispose();
    }

    private void SendMegaBroadcastStruct()
    {
        using (SubStream streamA = SubStream.StartWriting(this.NetworkManager, out PooledWriter writerA))
        {
            using (SubStream streamB = SubStream.StartWriting(this.NetworkManager, out PooledWriter writerB))
            {
                WriteRandomDataToBuffer(writerA);
                WriteRandomDataToBuffer(writerB);

                var data = new MegaBroadcastStruct()
                {
                    Integer = 100,
                    FP = 420f,
                    Text = "FISHNET",
                    StreamA = streamA,
                    StreamB = streamB
                };

                ServerManager.Broadcast<MegaBroadcastStruct>(data);
            }
        }
    }

    private void WriteRandomDataToBuffer(Writer writer)
    {
        int randomLength = Random.Range(1, 100);

        writer.WriteInt32(randomLength);

        for (int i = 0; i < randomLength; i++)
        {
            writer.WriteInt32(i*i);
        }

        writer.WriteInt32(100);
        writer.WriteSingle(420f);
        writer.WriteString("hello fishworld!");
    }

    private void VerifyRandomDataFromBuffer(Reader reader)
    {
        int randomLength = reader.ReadInt32();

        for (int i = 0; i < randomLength; i++)
        {
            int value = reader.ReadInt32();

            if (value != i*i)
            {
                Debug.LogError("Value is not equal to index");
            }
        }

        if(reader.ReadInt32() != 100)
        {
            Debug.LogError("Integer is not equal to 100");
        }

        if(reader.ReadSingle() != 420f)
        {
            Debug.LogError("Float is not equal to 420f");
        }

        if(reader.ReadString() != "hello fishworld!")
        {
            Debug.LogError("String is not equal to hello fishworld!");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        ClientManager.RegisterBroadcast<BroadcastStruct>(ServerMessageReceivedA);
        ClientManager.RegisterBroadcast<BroadcastStruct>(ServerMessageReceivedB);
        ClientManager.RegisterBroadcast<MegaBroadcastStruct>(MegaBroadcastReceived);
    }

    private void ServerMessageReceivedA(BroadcastStruct data, Channel channel = Channel.Reliable)
    {
        ServerMessageReceivedParse(data.Stream);
    }

    private void ServerMessageReceivedB(BroadcastStruct data, Channel channel = Channel.Reliable)
    {
        ServerMessageReceivedParse(data.Stream);
    }

    private void ServerMessageReceivedParse(SubStream stream)
    {
        if (stream.StartReading(out Reader reader))
        {
            VerifyRandomDataFromBuffer(reader);
        }
    }

    private void MegaBroadcastReceived(MegaBroadcastStruct data, Channel channel = Channel.Reliable)
    {
        Debug.Log("MegaBroadcastReceived");
        // start reading StreamB on purpose before StreamA!
        if (data.StreamB.StartReading(out Reader readerB))
        {
            VerifyRandomDataFromBuffer(readerB);
        }

        if (data.StreamA.StartReading(out Reader readerA))
        {
            VerifyRandomDataFromBuffer(readerA);
        }

        /*
            Integer = 100,
            FP = 420f,
            Text = "Hello fishworld!",         
        */

        if (data.Integer != 100)
        {
            Debug.LogError("Integer is not equal to 100");
        }

        if (data.FP != 420f)
        {
            Debug.LogError("Float is not equal to 420f");
        }

        if (data.Text != "FISHNET")
        {
            Debug.LogError("String is not equal to FISHNET");
        }

    }
}
