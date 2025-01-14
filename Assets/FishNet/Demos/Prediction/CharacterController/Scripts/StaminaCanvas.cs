using UnityEngine;
using UnityEngine.UI;

namespace FishNet.Demo.Prediction.CharacterControllers
{
    /// <summary>
    /// This is a basic implementation of hooking up the UI to the owners character stamina.
    /// </summary>
    public class StaminaCanvas : MonoBehaviour
    {
        [SerializeField]
        private Image _staminaBar;
        
        private CharacterControllerPrediction _character;
        
        private void Awake()
        {
            CharacterControllerPrediction.OnOwner += CCP_OnOwner;
        }

        private void OnDestroy()
        {
            CharacterControllerPrediction.OnOwner -= CCP_OnOwner;
        }

        private void Update()
        {
            if (_character == null) return;

            float fillAmount = (_character.Stamina / CharacterControllerPrediction.Maximum_Stamina);
            _staminaBar.fillAmount = fillAmount;
        }

        private void CCP_OnOwner(CharacterControllerPrediction ccp)
        {
            _character = ccp;
        }
    }
}