﻿using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.ColliderRollback.Demo
{


    /// <summary>
    /// DEMO. CODE IS NOT OPTIMIZED.
    /// Moves the player around.
    /// </summary>
    public class PlayerMotor : NetworkBehaviour
    {
        [SerializeField]
        private float _moveRate = 3f;

        private CharacterController _characterController;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner)
                _characterController = GetComponent<CharacterController>();
        }


        private void Update()
        {
            if (base.IsOwner)
            {
                Move();
            }
        }

        private void Move()
        {
            if (_characterController == null)
                return;

            Vector3 gravity = new Vector3(0f, -10f, 0f);
            Vector3 inputs = transform.TransformDirection(
                new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"))
                );

            _characterController.Move((gravity + inputs) * _moveRate * Time.deltaTime);
        }
    }


}