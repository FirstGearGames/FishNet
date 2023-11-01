using FishNet.Managing.Transporting;
using System;

namespace FishNet.Example.IntermediateLayers
{
    /* Below is an example of creating a basic Caesar Cipher.
     * Bytes are modified by a set value of CIPHER_KEY, and then
     * the original src ArraySegment is returned.
     * 
     * It's very important to only iterate the bytes provided
     * as the segment. For example, if the ArraySegment contains
     * 1000 bytes but the Offset is 3 and Count is 5 then you should
     * only iterate bytes on index 3, 4, 5, 6, 7. The code below
     * shows one way of properly doing so.
     * 
     * If you are to change the byte array reference, size, or segment
     * count be sure to return a new ArraySegment with the new values.
     * For example, if your Offset was 0 and count was 10 but after
     * encrypting data the Offset was still 0 and count 15 you would
     * return new ArraySegment<byte>(theArray, 0, 15); */
    public class IntermediateLayerCipher : IntermediateLayer
    {
        private const byte CIPHER_KEY = 5;
        //Decipher incoming data.
        public override ArraySegment<byte> HandleIncoming(ArraySegment<byte> src, bool fromServer)
        {
            byte[] arr = src.Array;
            int length = src.Count;
            int offset = src.Offset;

            for (int i = src.Offset; i < (offset + length); i++)
            {
                short next = (short)(arr[i] - CIPHER_KEY);
                if (next < 0)
                    next += byte.MaxValue;
                arr[i] = (byte)next;
            }

            return src;
        }
        //Cipher outgoing data.
        public override ArraySegment<byte> HandleOutgoing(ArraySegment<byte> src, bool toServer)
        {
            byte[] arr = src.Array;
            int length = src.Count;
            int offset = src.Offset;

            for (int i = offset; i < (offset + length); i++)
            {
                short next = (short)(arr[i] + CIPHER_KEY);
                if (next > byte.MaxValue)
                    next -= byte.MaxValue;
                arr[i] = (byte)next;
            }

            return src;
        }

    }
}