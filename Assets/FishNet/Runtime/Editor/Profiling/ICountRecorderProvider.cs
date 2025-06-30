using FishNet.Utility.Performance.Profiling;

namespace Fishnet.NetworkProfiler.ModuleGUI
{
    internal interface ICountRecorderProvider
    {
        CountRecorder GetCountRecorder();
    }
}
