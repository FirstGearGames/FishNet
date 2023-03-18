using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened
{
    public struct UnloadedScene
    {
        public readonly string Name;
        public readonly int Handle;

        public UnloadedScene(Scene s)
        {
            Name = s.name;
            Handle = s.handle;
        }
        public UnloadedScene(string name, int handle)
        {
            Name = name;
            Handle = handle;
        }
    }
}
