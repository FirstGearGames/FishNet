using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Component.Transforming
{
    /// <summary> 
    /// Janky NetworkTransform. This is only for testing and will be replaced prior to release.
    /// </summary>   
    public class NetworkTransform : NetworkBehaviour
    { 
        [SerializeField]
        private bool _clientAuthoritative = true;
        [Tooltip("True to synchronize movements on server to owner when not using client authoritative movement.")]
        [SerializeField]
        private bool _synchronizeToOwner = true;

        private const float UPDATE_RATE = 0.02f;
        private const float INTERPOLATION = 0.025f;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _targetScale;

        private Vector3 _lastToClientsPosition;
        private Quaternion _lastToClientsRotation;
        private Vector3 _lastToClientsScale;
        private Vector3 _lastToServerPosition;
        private Quaternion _lastToServerRotation;
        private Vector3 _lastToServerScale;

        private float _positionRate;
        private float _scaleRate;
        private float _rotationRate;

        private float _nextClientSend = 0f;
        private float _nextServerSend = 0f;

        private bool _serverSentSettled = true;
        private bool _clientSentSettled = true;

        private void Awake()
        {
            SetTargets(transform.position, transform.rotation, transform.localScale);
            SetInstantRates();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SetTargets(transform.position, transform.rotation, transform.localScale);
        }

        public override void OnStartClient(bool isOwner)
        {
            base.OnStartClient(isOwner);
            SetTargets(transform.position, transform.rotation, transform.localScale);
        }
        private void SetTargets(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            _targetPosition = pos;
            _targetRotation = rot;
            _targetScale = scale;
        }

        private void Update()
        {
            MoveToTarget();
            CheckSendToClients();
            CheckSendToServer();
        }


        private void MoveToTarget()
        {
            //Not client authoritative, is owner, and don't sync to owner.
            if (!_clientAuthoritative && base.IsOwner && !_synchronizeToOwner)
                return;
            //Owner, client controls movement.
            if (_clientAuthoritative && base.IsOwner)
                return;
            //No owner, server controls movement.
            if (_clientAuthoritative && !base.OwnerIsValid)
                return;
            //Not client auth, server controls movement.
            if (!_clientAuthoritative && base.IsServer)
                return;

            if (_positionRate == -1f)
                transform.position = _targetPosition;
            else
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _positionRate * Time.deltaTime);

            if (_rotationRate == -1f)
                transform.rotation = _targetRotation;
            else
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation, _rotationRate * Time.deltaTime);

            if (_scaleRate == -1f)
                transform.localScale = _targetScale;
            else
                transform.localScale = Vector3.MoveTowards(transform.localScale, _targetScale, _scaleRate * Time.deltaTime);
        }

        private void CheckSendToClients()
        {
            if (!base.IsServer)
                return;
            if (Time.time < _nextServerSend)
                return;

            Channel channel = Channel.Unreliable;

            if (AllMatchTransform(ref _lastToClientsPosition, ref _lastToClientsRotation, ref _lastToClientsScale))
            {
                if (_serverSentSettled)
                    return;
                else
                    _serverSentSettled = true;

                channel = Channel.Reliable;
            }

            SetNextSendTime(ref _nextServerSend);

            if (base.IsOwner || !_clientAuthoritative)
                ObserversUpdateTransform(transform.position, transform.rotation, transform.localScale, channel);
            else
                ObserversUpdateTransform(_targetPosition, _targetRotation, _targetScale, channel);
        }

        private void CheckSendToServer()
        {
            if (!_clientAuthoritative || !base.IsOwner)
                return;
            if (Time.time < _nextClientSend)
                return;

            Channel channel = Channel.Unreliable;

            if (AllMatchTransform(ref _lastToServerPosition, ref _lastToServerRotation, ref _lastToServerScale))
            {
                if (_clientSentSettled)
                    return;
                else
                    _clientSentSettled = true;

                channel = Channel.Reliable;
            }

            SetNextSendTime(ref _nextClientSend);

            ServerUpdateTransform(transform.position, transform.rotation, transform.localScale, channel);
        }

        private void SetNextSendTime(ref float lastSetTime)
        {
            lastSetTime = Time.time + (UPDATE_RATE - Time.deltaTime);
        }

        private bool AllMatchTransform(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            bool match = (transform.position == position &&
                transform.rotation == rotation &&
                transform.localScale == scale);

            if (!match)
            {
                position = transform.position;
                rotation = transform.rotation;
                scale = transform.localScale;
            }

            return match;
        }

        #region Rates.
        private void SetInstantRates()
        {
            _positionRate = -1f;
            _rotationRate = -1f;
            _scaleRate = 1f;
        }

        private void SetTimedRates(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            float distance;
            float divisor = UPDATE_RATE + INTERPOLATION;
            distance = Vector3.Distance(transform.position, pos);
            _positionRate = distance / divisor;
            distance = Quaternion.Angle(transform.rotation, rot);
            _rotationRate = distance / divisor;
            distance = Vector3.Distance(transform.localScale, scale);
            _scaleRate = distance / divisor;
        }
        #endregion

        [ServerRpc]
        private void ServerUpdateTransform(Vector3 pos, Quaternion rot, Vector3 scale, Channel channel)
        {
            //Then set to snap to new position.
            if (base.IsServerOnly)
                SetInstantRates();
            else
                SetTimedRates(pos, rot, scale);

            SetTargets(pos, rot, scale);
        }

        [ObserversRpc]
        private void ObserversUpdateTransform(Vector3 pos, Quaternion rot, Vector3 scale, Channel channel)
        {
            if (!_clientAuthoritative && base.IsOwner && !_synchronizeToOwner)
                return;
            if (_clientAuthoritative && base.IsOwner)
                return;
            if (base.IsServer)
                return;

            SetTimedRates(pos, rot, scale);
            SetTargets(pos, rot, scale);
        }
    }


}