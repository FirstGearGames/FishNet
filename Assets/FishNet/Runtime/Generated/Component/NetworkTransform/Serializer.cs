//using FishNet.Serializing;

//namespace FishNet.Component.Transforming
//{ 

//public static class Serializer
//{
//        public static void WriteTransformBundle(this PooledWriter writer, TransformBundle value)
//        {
//            writer.WriteArraySegment(value.Data);
//        }

//        public static TransformBundle ReadTransformBundle(this PooledReader reader)
//        {
//            return new TransformBundle
//            {
//                Data = reader.ReadArraySegment(reader.Remaining)
//            };
//        }

//}

//}
