using FishNet.Managing;
using FishNet.Managing.Logging;
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    [ExecuteInEditMode]
    public class PredictedObject : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// Type of prediction movement being used.
        /// </summary>
        internal enum PredictionType : byte
        {
            Other = 0,
            Rigidbody = 1,
            Rigidbody2D = 2
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.
        /// </summary>
        [Tooltip("Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.")]
        [SerializeField]
        private Transform _graphicalObject;
        /// <summary>
        /// Duration to smooth desynchronizations over.
        /// </summary>
        [Tooltip("Duration to smooth desynchronizations over.")]
        [Range(0.01f, 0.5f)]
        [SerializeField]
        private float _smoothingDuration = 0.125f;
        /// <summary>
        /// Type of prediction movement which is being used.
        /// </summary>
        [Tooltip("Type of prediction movement which is being used.")]
        [SerializeField]
        private PredictionType _predictionType;
        /// <summary>
        /// Rigidbody to predict.
        /// </summary>
        [Tooltip("Rigidbody to predict.")]
        [SerializeField]
        private Rigidbody _rigidbody;
        /// <summary>
        /// Rigidbody2D to predict.
        /// </summary>
        [Tooltip("Rigidbody2D to predict.")]
        [SerializeField]
        private Rigidbody2D _rigidbody2d;
        /// <summary>
        /// How much of the previous velocity to retain when predicting. Default value is 0f. Increasing this value may result in overshooting with rigidbodies that do not behave naturally, such as controllers or vehicles.
        /// </summary>
        [Tooltip("How much of the previous velocity to retain when predicting. Default value is 0f. Increasing this value may result in overshooting with rigidbodies that do not behave naturally, such as controllers or vehicles.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _predictionRatio = 0f;
        #endregion

        #region Editor Private. 
        [SerializeField, HideInInspector]
        private Transform _lastGraphicalObject;
        [SerializeField, HideInInspector]
        private PredictionType _lastMovementType;
        [SerializeField, HideInInspector]
        private UnityEngine.Component _lastSelectedRigidbody;
        [SerializeField, HideInInspector]
        private PredictedRigidbodyBase _addedPredictedRigidbody;
        [SerializeField, HideInInspector]
        private DesyncSmoother _addedDesyncSmoother;
        [SerializeField, HideInInspector]
        private bool _rebuildComponents;
        #endregion 

        private void Awake()
        {
            if (Application.isPlaying)
                InitializeOnce();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            //Using update because delayCall doesn't work properly.
            SubscribeToEditorUpdate(false);
            RemoveAddedScripts(_graphicalObject);
#endif
        }

        /// <summary>
        /// Changes subscription to editor update.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToEditorUpdate(bool subscribe)
        {
#if UNITY_EDITOR
            if (subscribe)
            {
                EditorApplication.update -= new EditorApplication.CallbackFunction(OnEditor_Update);
                EditorApplication.update += new EditorApplication.CallbackFunction(OnEditor_Update);
            }
            else
            {
                EditorApplication.update -= new EditorApplication.CallbackFunction(OnEditor_Update);
            }
#endif
        }
        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void InitializeOnce()
        {
            //No graphical object, cannot smooth.
            if (_graphicalObject == null)
            {
                if (NetworkManager.StaticCanLog(LoggingType.Error))
                    Debug.LogError($"GraphicalObject is not set on {gameObject.name}. Initialization will fail.");
                return;
            }

            //Rb settings.
            if (_predictionType == PredictionType.Rigidbody)
                _addedPredictedRigidbody.SetRigidbody(_rigidbody);
            else if (_predictionType == PredictionType.Rigidbody2D)
                _addedPredictedRigidbody.SetRigidbody(_rigidbody2d);
            _addedPredictedRigidbody?.SetPredictionRatio(_predictionRatio);
        }


#if UNITY_EDITOR
        private void OnEditor_Update()
        {
            if (_rebuildComponents)
            {
                _rebuildComponents = false;
                SubscribeToEditorUpdate(false);
                RemoveAddedScripts(_lastGraphicalObject);
                _lastMovementType = _predictionType;
                _lastGraphicalObject = _graphicalObject;
                AddScripts();
            }
        }

        /// <summary>
        /// Removes scripts added by this component.
        /// </summary>
        private void RemoveAddedScripts(Transform t)
        {
            //Cannot find scripts if graphical object is null.
            if (t == null)
                return;

            UnityEngine.Component c;
            //rbs
            PredictedRigidbodyBase[] pbrs = t.GetComponents<PredictedRigidbodyBase>();
            foreach (PredictedRigidbodyBase item in pbrs)
                DestroyImmediate(item, true);
            //DesyncSmoother.
            c = t.GetComponent<DesyncSmoother>();
            if (c != null)
                DestroyImmediate(c, true);
        }

        /// <summary>
        /// Adds scripts for the current settings.
        /// </summary>
        private void AddScripts()
        {
            //Nothing to add onto.
            if (_graphicalObject == null)
                return;

            GameObject go = _graphicalObject.gameObject;
            //DesyncSmoother always gets added.
            _addedDesyncSmoother = go.AddComponent<DesyncSmoother>();
            _addedDesyncSmoother.SetSmoothingDuration(_smoothingDuration);

            if (_predictionType == PredictionType.Rigidbody)
                _addedPredictedRigidbody = go.AddComponent<PredictedRigidbody>();
            else if (_predictionType == PredictionType.Rigidbody2D)
                _addedPredictedRigidbody = go.AddComponent<PredictedRigidbody2D>();
        }

        private void OnValidate()
        {
            if (_graphicalObject != null && _graphicalObject.parent == null)
            {
                Debug.LogError($"The graphical object may not be the root of the transform. Your graphical objects must be beneath your prediction scripts so that they may be smoothed independently during desynchronizations.");
                _graphicalObject = null;
                RemoveAddedScripts(_graphicalObject);
                return;
            }

            _rebuildComponents |= (_predictionType != _lastMovementType);
            _rebuildComponents |= (_graphicalObject != _lastGraphicalObject);

            if (_rebuildComponents)
            {
                SubscribeToEditorUpdate(true);
                if (!gameObject.activeInHierarchy && _graphicalObject != null)
                {
                    string scriptName = GetType().Name;
                    Debug.LogWarning($"GameObject {gameObject.name} is disabled. Should you remove component {scriptName} while the object is disabled, supporting scripts will not clean up properly." +
                        $" Be sure to enable this gameObject and it's root before removing {scriptName}, or manually remove added scripts from {_graphicalObject.name}.");
                }
            }
        }

#endif
    }


}