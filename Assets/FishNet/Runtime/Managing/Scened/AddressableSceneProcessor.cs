using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace GameSystem.Core
{
    public class AddressableSceneProcessor : DefaultSceneProcessor
    {
        [SerializeField] private string _lobbySceneName = "LobbyGameScene";
        [SerializeField] private List<string> _unloadExcludedScenes;
        private List<AsyncOperationHandle<SceneInstance>> _loadAsyncHandles = new();
        private AsyncOperationHandle<SceneInstance> _currentAsyncHandle;

        public override void LoadStart(LoadQueueData queueData)
        {
            base.LoadStart(queueData);
            ResetValues();
        }

        public override void LoadEnd(LoadQueueData queueData)
        {
            base.LoadEnd(queueData);
            ResetValues();
        }

        private void ResetValues() => _currentAsyncHandle = default;

        public override void BeginLoadAsync(string sceneName, LoadSceneParameters parameters)
        {
            var ao = Addressables.LoadSceneAsync(sceneName, parameters);
            _loadAsyncHandles.Add(ao);
            _currentAsyncHandle = ao;
        }

        public override void BeginUnloadAsync(Scene scene)
        {
            if (_unloadExcludedScenes.Contains(scene.name))
                return;

            if (scene.name != _lobbySceneName)
            {
                base.BeginUnloadAsync(scene);
                return;
            }

            var handle = _loadAsyncHandles
                .FirstOrDefault(h => h.IsValid() && h.Result.Scene == scene);

            Debug.Log($"[{nameof(AddressableSceneProcessor)}] Async Addressable Scene Unloading: {scene.name}");

            _currentAsyncHandle = Addressables.UnloadSceneAsync(handle);

            _loadAsyncHandles.Remove(handle);
        }

        public override float GetPercentComplete() => _currentAsyncHandle.IsValid() ? _currentAsyncHandle.PercentComplete : 1f;

        public override async UniTask AsyncsIsDone()
        {
            foreach (var ao in _loadAsyncHandles)
                await UniTask.WaitUntil(() => ao.IsDone);
        }
    }
}