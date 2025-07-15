using UnityEngine;
using UnityEngine.UI;

namespace FishNet.Example.ColliderRollbacks
{
    public class TextCanvas : MonoBehaviour
    {
        [SerializeField]
        private Text _text;
        private static TextCanvas _instance;

        private void Awake()
        {
            if (_instance != null)
                Destroy(_instance.gameObject);

            _instance = this;
        }

        public void SetText(string text)
        {
            _text.text = text;
        }
    }
}