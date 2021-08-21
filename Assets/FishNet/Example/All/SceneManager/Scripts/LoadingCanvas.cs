using FishNet;
using FishNet.Managing.Scened.Eventing;
using UnityEngine;
using UnityEngine.UI;
namespace FirstGearGames.FlexSceneManager.Demos
{

    public class LoadingCanvas : MonoBehaviour
    {

        [SerializeField]
        private Image _loadingBar = null;

        private static LoadingCanvas _instance;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            InstanceFinder.SceneManager.OnSceneQueueStart += SceneManager_OnSceneQueueStart;
            InstanceFinder.SceneManager.OnSceneQueueEnd += ceneManager_OnSceneQueueEnd;
            InstanceFinder.SceneManager.OnLoadScenePercentChange += SceneManager_OnLoadScenePercentChange;
            gameObject.SetActive(false);

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void SceneManager_OnLoadScenePercentChange(LoadScenePercentEventArgs obj)
        {
            _loadingBar.fillAmount = obj.Percent;
        }

        private void ceneManager_OnSceneQueueEnd()
        {
            gameObject.SetActive(false);
        }

        private void SceneManager_OnSceneQueueStart()
        {
            _loadingBar.fillAmount = 0f;
            gameObject.SetActive(true);
        }


    }


}