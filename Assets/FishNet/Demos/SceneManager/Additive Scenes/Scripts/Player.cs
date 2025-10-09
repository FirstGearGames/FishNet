using FishNet.Connection;
using FishNet.Object;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishNet.Demo.AdditiveScenes
{
    public class Player : NetworkBehaviour
    {
        [SerializeField]
        private Transform _ownerObjects;
        [SerializeField]
        private float _moveRate = 2f;
        private List<Waypoint> _wayPoints = new();
        private int _goalIndex;
        private Vector3 _goalOffset;


        private bool _foundWaypoints;

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            _ownerObjects.gameObject.SetActive(IsOwner);
        }


        private void Update()
        {
            if (!TryFindWaypoints())
                return;

            Vector3 posGoal = _wayPoints[_goalIndex].transform.position + _goalOffset;
            transform.position = Vector3.MoveTowards(transform.position, posGoal, _moveRate * Time.deltaTime);

            Vector3 lookDirection = (posGoal - transform.position).normalized;
            // Rotate to goal if there is a look direction.
            if (lookDirection != Vector3.zero)
            {
                Quaternion rot = Quaternion.LookRotation((posGoal - transform.position).normalized, transform.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, 270f * Time.deltaTime);
            }

            // If at goal set next goal.
            if (transform.position == posGoal)
            {
                _goalIndex++;
                // Reset index to 0 if at last goal.
                if (_goalIndex >= _wayPoints.Count)
                    _goalIndex = 0;
            }
        }
        
        
        private bool TryFindWaypoints()
        {
            if (_foundWaypoints)
                return true;
            //Only the server uses waypoints.
            if (!IsServerStarted)
                return false;
            
            _wayPoints = FindObjectsOfType<Waypoint>().ToList();
            /* There are expected 4 waypoints in this test -- if only
             * one is found then the other scenes did not load yet. */
            if (_wayPoints.Count != 4)
                return false;
            
            /* Stagger spawn position slightly depending on player count.
             * Also inverse direction so players cross each other when more
             * than one. This is just demo fanciness. */
            if (ServerManager.Clients.Count % 2 == 0)
            {
                _goalOffset = new(-0.5f, 0f, 0f);
                _wayPoints = _wayPoints.OrderBy(x => x.WaypointIndex).ToList();
            }
            else
            {
                _goalOffset = new(0.5f, 0f, 0f);
                _wayPoints = _wayPoints.OrderByDescending(x => x.WaypointIndex).ToList();
            }

            // Snap to current waypoint.
            transform.position = _wayPoints[0].transform.position + _goalOffset;
            // Set goal to next waypoint.
            _goalIndex = 1;

            _foundWaypoints = true;

            return true;
        }
    }
}