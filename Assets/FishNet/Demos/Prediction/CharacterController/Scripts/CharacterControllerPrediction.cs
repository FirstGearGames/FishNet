#if !FISHNET_STABLE_REPLICATESTATES
using System;
using FishNet.Component.Prediction;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace FishNet.Demo.Prediction.CharacterControllers
{
    public class CharacterControllerPrediction : TickNetworkBehaviour
    {
        #region Types.
        /// <summary>
        /// One-time inputs accumulated over frames between ticks.
        /// </summary>
        public struct OneTimeInput
        {
            /// <summary>
            /// True to jump.
            /// </summary>
            public bool Jump;

            /// <summary>
            /// Unset inputs.
            /// </summary>
            public void ResetState()
            {
                Jump = false;
            }
        }

        public struct ReplicateData : IReplicateData
        {
            public ReplicateData(Vector2 input, bool run, OneTimeInput oneTimeInputs)
            {
                OneTimeInputs = oneTimeInputs;
                Input = input;
                Run = run;

                _tick = 0;
            }

            /// <summary>
            /// True if Jump input was pressed for the tick.
            /// </summary>
            public OneTimeInput OneTimeInputs;
            /// <summary>
            /// Current movement directions held.
            /// </summary>
            public Vector2 Input;
            /// <summary>
            /// True if run is held.
            /// </summary>
            public readonly bool Run;
            /// <summary>
            /// Tick is set at runtime. There is no need to manually assign this value.
            /// </summary>
            private uint _tick;

            public void Dispose()
            {
                OneTimeInputs.ResetState();
            }

            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public ReconcileData(Vector3 position, float verticalVelocity, float stamina, MovingPlatform currentPlatform)
            {
                Position = position;
                VerticalVelocity = verticalVelocity;
                Stamina = stamina;
                CurrentPlatform = currentPlatform;

                _tick = 0;
            }

            /// <summary>
            /// Position of the character.
            /// </summary>
            public Vector3 Position;
            /// <summary>
            /// Current vertical velocity.
            /// </summary>
            /// <remarks>Used to simulate jumps and falls.</remarks>
            public float VerticalVelocity;
            /// <summary>
            /// Amount of stamina remaining to run or jump.
            /// </summary>
            public float Stamina;
            public MovingPlatform CurrentPlatform;
            /// <summary>
            /// Tick is set at runtime. There is no need to manually assign this value.
            /// </summary>
            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        /// <summary>
        /// Invokes whenever NetworkStart is called for owner.
        /// </summary>
        public static event Action<CharacterControllerPrediction> OnOwner;
        /// <summary>
        /// Current stamina remaining.
        /// </summary>
        public float Stamina { get; private set; }
        /// <summary>
        /// Amount of force for jumping.
        /// </summary>
        [Tooltip("Amount of force for jumping.")]
        [SerializeField]
        private float _jumpForce = 30f;
        /// <summary>
        /// How quickly to move.
        /// </summary>
        [Tooltip("How quickly to move.")]
        [SerializeField]
        private float _moveRate = 4f;
        /// <summary>
        /// Current vertical velocity.
        /// </summary>
        private float _verticalVelocity;
        /// <summary>
        /// One-time inputs accumulated over frames between ticks.
        /// </summary>
        private OneTimeInput _oneTimeInputs = new();
        /// <summary>
        /// Last data which was supplied during replicate outside of reconcile.
        /// </summary>
        private ReplicateData _lastTickedReplicateData = default;
        /// <summary>
        /// Current platform the player is on.
        /// </summary>
        private MovingPlatform _currentPlatform;
        /// <summary>
        /// Reference to the CharacterController component.
        /// </summary>
        private CharacterController _characterController;
        /// <summary>
        /// NetworkTrigger on this character; used to detact platforms.
        /// </summary>
        private NetworkTrigger _characterTrigger;
        /// <summary>
        /// maximum amount of stamina allowed.
        /// </summary>
        public const float Maximum_Stamina = 50f;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            _characterTrigger = GetComponentInChildren<NetworkTrigger>();
            _characterTrigger.OnEnter += CharacterTrigger_OnEnter;
            _characterTrigger.OnExit += CharacterTrigger_OnExit;

            // We only need the OnTick callback for non-physics.
            SetTickCallbacks(TickCallback.Tick);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            if (IsOwner)
                OnOwner?.Invoke(this);
        }

        private void Update()
        {
            SetOneTimeInputs();
        }

        /// <summary>
        /// Checks setting inputs which are one-time(not held).
        /// </summary>
        private void SetOneTimeInputs()
        {
            if (!IsOwner)
                return;

            /* Check to jump. */
            if (Input.GetKeyDown(KeyCode.Space))
                _oneTimeInputs.Jump = true;
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildMoveData());
            CreateReconcile();
        }

        /// <summary>
        /// Returns replicate data to send as the controller.
        /// </summary>
        private ReplicateData BuildMoveData()
        {
            /* Only the controller needs to build move data.
             * This could be the server if the server if no owner, for example
             * such as AI, or the owner of the object. */
            if (!IsOwner)
                return default;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            // Run when left shift is held.
            bool run = Input.GetKey(KeyCode.LeftShift);

            ReplicateData md = new(new(horizontal, vertical), run, _oneTimeInputs);

            // Reset one tine inputs since they've been processed for the tick.
            _oneTimeInputs.ResetState();

            return md;
        }

        /// <summary>
        /// Creates a reconcile that is sent to clients.
        /// </summary>
        public override void CreateReconcile()
        {
            /* Both the server and client should create reconcile data.
             * The client will use their copy as a fallback if they do not
             * get data from the server, such as a dropped packet.
             *
             * The client will not reconcile unless it receives at least one
             * reconcile packet from the server for the tick. */

            /* You do not have to reconcile every tick if you wish to
             * save bandwidth/perf, or simply feel as though it's not needed
             * for your game type.
             *
             * Even when not reconciling every tick it's still recommended
             * to build the reconcile as client; this cost is very little.*/

            /* This is an example of only sending a reconcile occasionally
             * if the server. Simply uncomment the if statement below to
             * test this behavior. */
            // if (base.IsServerStarted)
            // {
            //     // Exit early if 10 ticks have not passed.
            //     if (base.TimeManager.LocalTick % 10 != 0) return;
            // }

            // Build the data using current information and call the reconcile method.
            ReconcileData rd = new(transform.localPosition, _verticalVelocity, Stamina, _currentPlatform);
            PerformReconcile(rd);
        }

        [Replicate]
        private void PerformReplicate(ReplicateData rd, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            // Always use the tickDelta as your delta when performing actions inside replicate.
            float delta = (float)TimeManager.TickDelta;
            bool useDefaultForces = false;

            /* When client only run some checks to
             * further predict the clients future movement.
             * This can keep the object more inlined with real-time by
             * guessing what the clients input might be before we
             * actually receive it.
             *
             * Doing this does risk a chance of graphical jitter in the
             * scenario a de-synchronization occurs, but if only predicting
             * a couple ticks the chances are low. */
            // See https://fish-networking.gitbook.io/docs/manual/guides/prediction/version-2/creating-code/predicting-states
            if (!IsServerStarted && !IsOwner)
            {
                /* If ticked then set last ticked value.
                 * Ticked means the replicate is being run from the tick cycle, more
                 * specifically NOT from a replay/reconcile. */
                if (state.ContainsTicked())
                {
                    /* Dispose of old should it have anything that needs to be cleaned up.
                     * If you are only using value types in your data you do not need to call Dispose.
                     * You must implement dispose manually to cache any non-value types, if you wish. */
                    _lastTickedReplicateData.Dispose();
                    // Set new.
                    _lastTickedReplicateData = rd;
                }
                /* In the future means there is no way the data can be known to this client
                 * yet. For example, the client is running this script locally and due to
                 * how networking works, they have not yet received the latest information from
                 * the server.
                 *
                 * If in the future then we are only going to predict up to
                 * a certain amount of ticks in the future. This is us assuming that the
                 * server (or client which owns this in this case) is going to use the
                 * same input for at least X number of ticks. You can predict none, or as many
                 * as you like, but the more inputs you predict the higher likeliness of guessing
                 * wrong. If you do however predict wrong often smoothing will cover up the mistake. */
                else if (state.IsFuture())
                {
                    /* Predict up to 1 tick more. */
                    if (rd.GetTick() - _lastTickedReplicateData.GetTick() > 1)
                    {
                        useDefaultForces = true;
                    }
                    else
                    {
                        /* If here we are predicting the future. */

                        /* You likely do not need to dispose rd here since it would be default
                         * when state is 'not created'. We are simply doing it for good practice, should your ReplicateData
                         * contain any garbage collection. */
                        rd.Dispose();

                        rd = _lastTickedReplicateData;

                        /* There are some fields you might not want to predict, for example
                         * jump. The odds of a client pressing jump two ticks in a row is unlikely.
                         * The stamina check below would likely prevent such a scenario.
                         *
                         * We're going to unset jump for this reason. */
                        rd.OneTimeInputs.Jump = false;

                        /* Be aware that future predicting is not a one-size fits all
                         * feature. How much you predict into the future, if at all, depends
                         * on your game mechanics and your desired outcome. */
                    }
                }
            }

            Vector3 forces;

            if (useDefaultForces)
            {
                /* Character controllers are a bit problematic with colliders.
                 * If you were to pass Vector3.zero into the move then there's
                 * a chance other colliders will clip through the characterController.
                 * When this is combined with reconciles, it's practically gauranteed
                 * this will happen.
                 *
                 * Because of this issue, if we are 'using default forces' we will apply a
                 * very insignificant amount of force which makes the characterController
                 * update properly. */
                forces = new(0f, -1f, 0f);
            }
            // Calculate forces.
            else
            {
                // Stamina regained over every second.
                const float regainedStamina = 25f;
                // Add stamina with every tick.
                ModifyStamina(regainedStamina * delta);

                // Add gravity. Extra gravity is added for snappier jumps.
                _verticalVelocity += Physics.gravity.y * delta * 3f;
                //Cap gravity to -20f so the player doesn't fall too fast.
                if (_verticalVelocity < -40f)
                    _verticalVelocity = -40f;

                //Normalize direction so the player does not move faster at angles.
                rd.Input = rd.Input.normalized;

                /* Typically speaking any modification which can affect your CSP (prediction) should occur
                 * inside replicate. This is why we add/remove stamina, and move within replicate. */

                //Default run multiplier.
                float runMultiplier;
                //Stamina required to run over a second.
                const float runStamina = 50f;
                if (rd.Run && TryRemoveStamina(runStamina * delta))
                    runMultiplier = 1.5f;
                else
                    runMultiplier = 1f;

                //Stamina required to jump.
                const byte jumpStamina = 30;
                /* For consistent jumps set to jump force when jumping, rather
                 * than add force onto current gravity. */
                if (rd.OneTimeInputs.Jump && TryRemoveStamina(jumpStamina))
                    _verticalVelocity = _jumpForce;

                forces = new Vector3(rd.Input.x, 0f, rd.Input.y) * (_moveRate * runMultiplier);
                //Add vertical velocity to forces.
                forces.y = _verticalVelocity;
            }

            _characterController.Move(forces * delta);
        }

        [Reconcile]
        private void PerformReconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            /* Simply set current values to as they are
             * in the reconcile data. */
            _verticalVelocity = rd.VerticalVelocity;
            Stamina = rd.Stamina;

            /* Even though the platform is traced for in replicate we must also
             * pass the current platform into the reconcile, and set our local value
             * to whatever is provided in the reconcile.
             *
             * This is done because we use local position to reset the character
             * and if the clients currentPlatform differs from what the server
             * had when reconciling the world position would be significantly
             * different due to the parent not aligning. */
            _currentPlatform = rd.CurrentPlatform;
            //Set transform parent after assigning current.
            SetParent();

            /* Update position AFTER setting the parent, otherwise
             * you would face a potentially huge positional de-sync
             * as mentioned above. */
            transform.localPosition = rd.Position;
        }

        /// <summary>
        /// Called when the trigger on this object enters another collider.
        /// </summary>
        private void CharacterTrigger_OnEnter(Collider c)
        {
            //We only care about moving platforms.
            if (!c.TryGetComponent(out MovingPlatform mp))
                return;

            _currentPlatform = mp;
            SetParent();
        }

        /// <summary>
        /// Called when the trigger on this object exits another collider.
        /// </summary>
        private void CharacterTrigger_OnExit(Collider c)
        {
            if (!c.TryGetComponent(out MovingPlatform mp))
                return;

            //Only check if already attached to a platform.
            if (_currentPlatform == null)
                return;
            //Not the current platform.
            if (_currentPlatform != mp)
                return;

            //Is the current platform, unset.
            _currentPlatform = null;
            SetParent();
        }

        /// <summary>
        /// Updates parent state based on current platforms value.
        /// </summary>
        private void SetParent()
        {
            if (_currentPlatform != null)
                transform.SetParent(_currentPlatform.transform);
            else
                transform.SetParent(null);
        }

        /// <summary>
        /// Modifies stamina by adding or removing stamina.
        /// </summary>
        private void ModifyStamina(float value)
        {
            float next = Stamina + value;
            Stamina = Mathf.Clamp(next, 0f, Maximum_Stamina);
        }

        /// <summary>
        /// Removes stamina if enough stamina is available.
        /// </summary>
        /// <returns>>True if stamina was available and removed.</returns>
        private bool TryRemoveStamina(float value)
        {
            if (Stamina < value)
                return false;

            Stamina -= value;
            return true;
        }
    }
}
#endif