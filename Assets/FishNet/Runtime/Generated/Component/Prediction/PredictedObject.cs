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
        private float _lastPredictionRatio;
        [SerializeField, HideInInspector]
        private Transform _lastGraphicalObject;
        [SerializeField, HideInInspector]
        private PredictionType _lastMovementType;
        [SerializeField, HideInInspector]
        private float _lastSmoothingDuration;
        [SerializeField, HideInInspector]
        private UnityEngine.Component _lastSelectedRigidbody;
        [SerializeField, HideInInspector]
        private UnityEngine.Component _addedPredictedRigidbody;
        [SerializeField, HideInInspector]
        private DesyncSmoother _addedDesyncSmoother;
        [SerializeField, HideInInspector]
        private bool _rebuildComponents;
        #endregion

        private void Awake()
        {
#if UNITY_EDITOR
            //Using update because delayCall doesn't work properly.
            EditorApplication.update += new EditorApplication.CallbackFunction(OnEditor_DelayCall);
#endif
            if (Application.isPlaying)
                InitializeOnce();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            //Using update because delayCall doesn't work properly.
            EditorApplication.update -= new EditorApplication.CallbackFunction(OnEditor_DelayCall);
            Cleanup();
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
            //RB component not specified.
            if ((_predictionType == PredictionType.Rigidbody2D && _rigidbody2d == null) ||
                (_predictionType == PredictionType.Rigidbody && _rigidbody == null))
            {
                if (NetworkManager.StaticCanLog(LoggingType.Error))
                    Debug.LogError($"MovementType on {gameObject.name} uses a physics object but rigidbody or rigidbody2d is not set. Initialization will fail.");
                return;
            }
        }


#if UNITY_EDITOR
        private void OnEditor_DelayCall()
        {
            if (_rebuildComponents)
            {
                _rebuildComponents = false;
                Cleanup();
                _lastMovementType = _predictionType;
                _lastGraphicalObject = _graphicalObject;
                AddScripts();
            }
        }
        /// <summary>
        /// Cleans up this object and all script it added.
        /// </summary>
        internal void Cleanup()
        {
            RemoveAddedScripts();
        }

        /// <summary>
        /// Removes scripts added by this component.
        /// </summary>
        private void RemoveAddedScripts()
        {
            //Cannot find scripts if graphical object is null.
            if (_graphicalObject == null)
                return;

            //Remove them all, it's safer.
            UnityEngine.Component c;

            //rb
            c = _graphicalObject.GetComponent<PredictedRigidbody>();
            if (c != null)
                DestroyImmediate(c);
            //rb2d
            c = _graphicalObject.GetComponent<PredictedRigidbody2D>();
            if (c != null)
                DestroyImmediate(c);
            //DesyncSmoother.
            c = _graphicalObject.GetComponent<DesyncSmoother>();
            if (c != null)
                DestroyImmediate(c);
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
            {
                PredictedRigidbody pr = go.AddComponent<PredictedRigidbody>();
                _addedPredictedRigidbody = pr;
                pr.SetRigidbody(_rigidbody);
            }
            else if (_predictionType == PredictionType.Rigidbody2D)
            {
                PredictedRigidbody2D pr = go.AddComponent<PredictedRigidbody2D>();
                _addedPredictedRigidbody = pr;
                pr.SetRigidbody2D(_rigidbody2d);
            }
        }

        private void OnValidate()
        {
            if (_graphicalObject != null && _graphicalObject.parent == null)
            {
                Debug.LogError($"The graphical object may not be the root of the transform. Your graphical objects must be beneath your prediction scripts so that they may be smoothed independently during desynchronizations.");
                _graphicalObject = null;
                Cleanup();
                return;
            }

            _rebuildComponents |= (_predictionType != _lastMovementType);
            _rebuildComponents |= (_graphicalObject != _lastGraphicalObject);

            //Rigidbody change.
            if (_predictionType == PredictionType.Rigidbody && _rigidbody != _lastSelectedRigidbody)
            {
                if (_addedPredictedRigidbody is PredictedRigidbody pr0)
                    pr0.SetRigidbody(_rigidbody);
            }
            //Rigidbody2D change.
            if (_predictionType == PredictionType.Rigidbody2D && _rigidbody2d != _lastSelectedRigidbody)
            {
                if (_addedPredictedRigidbody is PredictedRigidbody2D pr1)
                    pr1.SetRigidbody2D(_rigidbody2d);
            }

            //Prediction ratio change.
            if (_predictionRatio != _lastPredictionRatio)
            {
                _lastPredictionRatio = _predictionRatio;
                if (_addedPredictedRigidbody != null)
                {
                    PredictedRigidbodyBase prb = (PredictedRigidbodyBase)_addedPredictedRigidbody;
                    prb.SetPredictionRatio(_predictionRatio);
                }
            }
            //Smoothing duration change.
            if (_smoothingDuration != _lastSmoothingDuration)
            {
                _lastSmoothingDuration = _smoothingDuration;
                if (_addedDesyncSmoother != null)
                    _addedDesyncSmoother.SetSmoothingDuration(_smoothingDuration);
                    
            }
        }


#endif
    }


}