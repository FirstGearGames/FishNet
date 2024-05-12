#if UNITYMATHEMATICS

using System.Runtime.CompilerServices;

using Unity.Mathematics;

namespace FishNet.Serializing {

    public partial class Writer {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Writehalf(half value) {
            WriteUInt16(value.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Writehalf2(half2 value) {
            WriteUInt16(value.x.value);
            WriteUInt16(value.y.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Writehalf3(half3 value) {
            WriteUInt16(value.x.value);
            WriteUInt16(value.y.value);
            WriteUInt16(value.z.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Writehalf4(half4 value) {

            WriteUInt16(value.x.value);
            WriteUInt16(value.y.value);
            WriteUInt16(value.z.value);
            WriteUInt16(value.w.value);
        }
    }

    public partial class Reader {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public half Readhalf() { 
            return new half { value = ReadUInt16() };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public half2 Readhalf2() {

            half2 h = default;

            h.x.value = ReadUInt16();
            h.y.value = ReadUInt16();

            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public half3 Readhalf3() {

            half3 h = default;
            
            h.x.value = ReadUInt16();
            h.y.value = ReadUInt16();
            h.z.value = ReadUInt16();

            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public half4 Readhalf4() {

            half4 h = default;

            h.x.value = ReadUInt16();
            h.y.value = ReadUInt16();
            h.z.value = ReadUInt16();
            h.w.value = ReadUInt16();

            return h;
        }
    }
}

#endif