using FishNet.Documenting;
using System;
using System.Collections.Generic;

namespace FishNet.Component.Animating
{
    [APIExclude]
    [System.Serializable]
    internal struct AnimatorUpdate
    {
        public byte ComponentIndex;
        public uint ObjectId;
        public ArraySegment<byte> Data;
        public AnimatorUpdate(byte componentIndex, uint objectId, ArraySegment<byte> data)
        {
            ComponentIndex = componentIndex;
            ObjectId = objectId;
            Data = data;
        }
    }

    //internal struct ParameterUpdates
    //{
    //    public List<LayerWeightUpdate> LayerWeights;
    //    public List<SpeedUpdate> Speeds;
    //    public List<LayerStateUpdate> LayerStates;
    //    public List<BooleanUpdate> Bools;
    //    public List<FloatUpdate> Floats;
    //    public List<IntUpdate> Ints;
    //    public List<TriggerUpdate> Triggers;

    //    public void MakeNewList()
    //    {
    //        LayerWeights = new List<LayerWeightUpdate>() { new LayerWeightUpdate() };
    //        Speeds = new List<SpeedUpdate>() { new SpeedUpdate() };
    //        LayerStates = new List<LayerStateUpdate>() { new LayerStateUpdate() };
    //        Bools = new List<BooleanUpdate>() { new BooleanUpdate() };
    //        Floats = new List<FloatUpdate>() { new FloatUpdate() };
    //        Ints = new List<IntUpdate>() { new IntUpdate() };
    //        Triggers = new List<TriggerUpdate>() { new TriggerUpdate() };
    //    }

    //}
    //internal struct LayerWeightUpdate
    //{
    //    public byte Layer;
    //    public float Weight;

    //    public LayerWeightUpdate(byte layer, float weight)
    //    {
    //        Layer = layer;
    //        Weight = weight;
    //    }
    //}

    //internal struct SpeedUpdate
    //{
    //    public float Speed;

    //    public SpeedUpdate(float speed)
    //    {
    //        Speed = speed;
    //    }
    //}

    //internal struct LayerStateUpdate
    //{
    //    public byte Layer;
    //    public int Hash;
    //    public float Time;

    //    public LayerStateUpdate(byte layer, int hash, float time)
    //    {
    //        Layer = layer;
    //        Hash = hash;
    //        Time = time;
    //    }
    //}

    //internal struct BooleanUpdate
    //{
    //    public byte ParameterIndex;
    //    public bool Value;

    //    public BooleanUpdate(byte parameterIndex, bool value)
    //    {
    //        ParameterIndex = parameterIndex;
    //        Value = value;
    //    }
    //}

    //internal struct FloatUpdate
    //{
    //    public byte ParameterIndex;
    //    public float Value;

    //    public FloatUpdate(byte parameterIndex, float value)
    //    {
    //        ParameterIndex = parameterIndex;
    //        Value = value;
    //    }
    //}

    //internal struct IntUpdate
    //{
    //    public byte ParameterIndex;
    //    public int Value;

    //    public IntUpdate(byte parameterIndex, int value)
    //    {
    //        ParameterIndex = parameterIndex;
    //        Value = value;
    //    }
    //}

    //internal struct TriggerUpdate
    //{
    //    public byte ParameterIndex;
    //    public bool Set;

    //    public TriggerUpdate(byte parameterIndex, bool set)
    //    {
    //        ParameterIndex = parameterIndex;
    //        Set = set;
    //    }
    //}

}