using FishNet.Managing.Scened.Broadcast;
using FishNet.Managing.Scened.Data;
using FishNet.Object;
using FishNet.Serializing;
using System.Runtime.InteropServices;

namespace FishNet.Runtime
{
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    public static class ScenedReaderWriters
    {
        public static void Write___FishNetu002EManagingu002EScenedu002EBroadcastu002ELoadScenesBroadcast(
          this PooledWriter pooledWriter,
          LoadScenesBroadcast value)
        {
            ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ELoadSceneQueueData(pooledWriter, value.SceneQueueData);
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EDatau002ELoadSceneQueueData(
          this PooledWriter pooledWriter,
          LoadSceneQueueData value)
        {
            if (value == null)
            {
                pooledWriter.WriteBoolean(true);
            }
            else
            {
                pooledWriter.WriteBoolean(false);
                ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ESingleSceneData(pooledWriter, value.SingleScene);
                ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002EAdditiveScenesData(pooledWriter, value.AdditiveScenes);
                ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ENetworkedScenesData(pooledWriter, value.NetworkedScenes);
            }
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EDatau002ESingleSceneData(
          this PooledWriter pooledWriter,
          SingleSceneData value)
        {
            if (value == null)
            {
                pooledWriter.WriteBoolean(true);
            }
            else
            {
                pooledWriter.WriteBoolean(false);
                ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceData(pooledWriter, value.SceneReferenceData);
                ScenedReaderWriters.Write___FishNetu002EObjectu002ENetworkObjectu005Bu005D(pooledWriter, value.MovedNetworkObjects);
            }
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceData(
          this PooledWriter pooledWriter,
          SceneReferenceData value)
        {
            if ((object)value == null)
            {
                pooledWriter.WriteBoolean(true);
            }
            else
            {
                pooledWriter.WriteBoolean(false);
                pooledWriter.WriteInt32(value.Handle);
                pooledWriter.WriteString(value.Name);
            }
        }

        public static void Write___FishNetu002EObjectu002ENetworkObjectu005Bu005D(
          this PooledWriter pooledWriter,
          NetworkObject[] value)
        {
            if (value == null)
            {
                int num = -1;
                pooledWriter.WritePackedWhole((ulong)(uint)num);
            }
            else
            {
                int length = value.Length;
                pooledWriter.WritePackedWhole((ulong)(uint)length);
                for (int index = 0; index < length; ++index)
                    pooledWriter.WriteNetworkObject(value[index]);
            }
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EDatau002EAdditiveScenesData(
          this PooledWriter pooledWriter,
          AdditiveScenesData value)
        {
            if (value == null)
            {
                pooledWriter.WriteBoolean(true);
            }
            else
            {
                pooledWriter.WriteBoolean(false);
                ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceDatau005Bu005D(pooledWriter, value.SceneReferenceDatas);
                ScenedReaderWriters.Write___FishNetu002EObjectu002ENetworkObjectu005Bu005D(pooledWriter, value.MovedNetworkObjects);
            }
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceDatau005Bu005D(
          this PooledWriter pooledWriter,
          SceneReferenceData[] value)
        {
            if (value == null)
            {
                int num = -1;
                pooledWriter.WritePackedWhole((ulong)(uint)num);
            }
            else
            {
                int length = value.Length;
                pooledWriter.WritePackedWhole((ulong)(uint)length);
                for (int index = 0; index < length; ++index)
                    ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceData(pooledWriter, value[index]);
            }
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EDatau002ENetworkedScenesData(
          this PooledWriter pooledWriter,
          NetworkedScenesData value)
        {
            if (value == null)
            {
                pooledWriter.WriteBoolean(true);
            }
            else
            {
                pooledWriter.WriteBoolean(false);
                pooledWriter.WriteString(value.Single);
                ScenedReaderWriters.Write___Systemu002EStringu005Bu005D(pooledWriter, value.Additive);
            }
        }

        public static void Write___Systemu002EStringu005Bu005D(
          this PooledWriter pooledWriter,
          string[] value)
        {
            if (value == null)
            {
                int num = -1;
                pooledWriter.WritePackedWhole((ulong)(uint)num);
            }
            else
            {
                int length = value.Length;
                pooledWriter.WritePackedWhole((ulong)(uint)length);
                for (int index = 0; index < length; ++index)
                    pooledWriter.WriteString(value[index]);
            }
        }

        public static LoadScenesBroadcast Read___FishNetu002EManagingu002EScenedu002EBroadcastu002ELoadScenesBroadcast(
          this PooledReader pooledReader)
        {
            return new LoadScenesBroadcast()
            {
                SceneQueueData = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ELoadSceneQueueData(pooledReader)
            };
        }

        public static LoadSceneQueueData Read___FishNetu002EManagingu002EScenedu002EDatau002ELoadSceneQueueData(
          this PooledReader pooledReader)
        {
            if (pooledReader.ReadBoolean())
                return (LoadSceneQueueData)null;
            return new LoadSceneQueueData()
            {
                SingleScene = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ESingleSceneData(pooledReader),
                AdditiveScenes = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002EAdditiveScenesData(pooledReader),
                NetworkedScenes = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ENetworkedScenesData(pooledReader)
            };
        }

        public static SingleSceneData Read___FishNetu002EManagingu002EScenedu002EDatau002ESingleSceneData(
          this PooledReader pooledReader)
        {
            if (pooledReader.ReadBoolean())
                return (SingleSceneData)null;
            return new SingleSceneData()
            {
                SceneReferenceData = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceData(pooledReader),
                MovedNetworkObjects = ScenedReaderWriters.Read___FishNetu002EObjectu002ENetworkObjectu005Bu005D(pooledReader)
            };
        }

        public static SceneReferenceData Read___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceData(
          this PooledReader pooledReader)
        {
            if (pooledReader.ReadBoolean())
                return (SceneReferenceData)null;
            return new SceneReferenceData()
            {
                Handle = pooledReader.ReadInt32(),
                Name = pooledReader.ReadString()
            };
        }

        public static NetworkObject[] Read___FishNetu002EObjectu002ENetworkObjectu005Bu005D(
          this PooledReader pooledReader)
        {
            int length = (int)pooledReader.ReadPackedWhole();
            if (length == -1)
                return (NetworkObject[])null;
            NetworkObject[] networkObjectArray = new NetworkObject[length];
            for (int index = 0; index < length; ++index)
                networkObjectArray[index] = pooledReader.ReadNetworkObject();
            return networkObjectArray;
        }

        public static AdditiveScenesData Read___FishNetu002EManagingu002EScenedu002EDatau002EAdditiveScenesData(
          this PooledReader pooledReader)
        {
            if (pooledReader.ReadBoolean())
                return (AdditiveScenesData)null;
            return new AdditiveScenesData()
            {
                SceneReferenceDatas = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceDatau005Bu005D(pooledReader),
                MovedNetworkObjects = ScenedReaderWriters.Read___FishNetu002EObjectu002ENetworkObjectu005Bu005D(pooledReader)
            };
        }

        public static SceneReferenceData[] Read___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceDatau005Bu005D(
          this PooledReader pooledReader)
        {
            int length = (int)pooledReader.ReadPackedWhole();
            if (length == -1)
                return (SceneReferenceData[])null;
            SceneReferenceData[] sceneReferenceDataArray = new SceneReferenceData[length];
            for (int index = 0; index < length; ++index)
                sceneReferenceDataArray[index] = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceData(pooledReader);
            return sceneReferenceDataArray;
        }

        public static NetworkedScenesData Read___FishNetu002EManagingu002EScenedu002EDatau002ENetworkedScenesData(
          this PooledReader pooledReader)
        {
            if (pooledReader.ReadBoolean())
                return (NetworkedScenesData)null;
            return new NetworkedScenesData()
            {
                Single = pooledReader.ReadString(),
                Additive = ScenedReaderWriters.Read___Systemu002EStringu005Bu005D(pooledReader)
            };
        }

        public static string[] Read___Systemu002EStringu005Bu005D(this PooledReader pooledReader)
        {
            int length = (int)pooledReader.ReadPackedWhole();
            if (length == -1)
                return (string[])null;
            string[] strArray = new string[length];
            for (int index = 0; index < length; ++index)
                strArray[index] = pooledReader.ReadString();
            return strArray;
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EBroadcastu002EUnloadScenesBroadcast(
          this PooledWriter pooledWriter,
          UnloadScenesBroadcast value)
        {
            ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002EUnloadSceneQueueData(pooledWriter, value.SceneQueueData);
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EDatau002EUnloadSceneQueueData(
          this PooledWriter pooledWriter,
          UnloadSceneQueueData value)
        {
            if (value == null)
            {
                pooledWriter.WriteBoolean(true);
            }
            else
            {
                pooledWriter.WriteBoolean(false);
                ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002EAdditiveScenesData(pooledWriter, value.AdditiveScenes);
                ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ENetworkedScenesData(pooledWriter, value.NetworkedScenes);
            }
        }

        public static UnloadScenesBroadcast Read___FishNetu002EManagingu002EScenedu002EBroadcastu002EUnloadScenesBroadcast(
          this PooledReader pooledReader)
        {
            return new UnloadScenesBroadcast()
            {
                SceneQueueData = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002EUnloadSceneQueueData(pooledReader)
            };
        }

        public static UnloadSceneQueueData Read___FishNetu002EManagingu002EScenedu002EDatau002EUnloadSceneQueueData(
          this PooledReader pooledReader)
        {
            if (pooledReader.ReadBoolean())
                return (UnloadSceneQueueData)null;
            return new UnloadSceneQueueData()
            {
                AdditiveScenes = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002EAdditiveScenesData(pooledReader),
                NetworkedScenes = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ENetworkedScenesData(pooledReader)
            };
        }

        public static void Write___FishNetu002EManagingu002EScenedu002EBroadcastu002EClientScenesLoadedBroadcast(
          this PooledWriter pooledWriter,
          ClientScenesLoadedBroadcast value)
        {
            ScenedReaderWriters.Write___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceDatau005Bu005D(pooledWriter, value.SceneDatas);
        }

        public static ClientScenesLoadedBroadcast Read___FishNetu002EManagingu002EScenedu002EBroadcastu002EClientScenesLoadedBroadcast(
          this PooledReader pooledReader)
        {
            return new ClientScenesLoadedBroadcast()
            {
                SceneDatas = ScenedReaderWriters.Read___FishNetu002EManagingu002EScenedu002EDatau002ESceneReferenceDatau005Bu005D(pooledReader)
            };
        }

    }
}
