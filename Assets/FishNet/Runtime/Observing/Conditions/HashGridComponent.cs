using FishNet.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace FishNet.Component.Observing
{ 
    public class HashGridComponent : NetworkBehaviour
    {
        static readonly ProfilerMarker s_UpdateGrid = new ProfilerMarker("0 - UpdateGrid()");
        public static HashGrid StaticHashGrid;
        public static Dictionary<int, List<int>> ConnToNearbyPairs = new Dictionary<int, List<int>>();

        [SerializeField] private int _cellSearchDistance;
        [SerializeField] private float _updateFrequency;
        private NetworkObject _networkObject;
        private int _ownerId;
        private Coroutine _updateGrid;
        private GridCell _currentCell = new GridCell();

        public override void OnStopServer()
        {
            base.OnStopServer();
            if(_updateGrid != null) StopCoroutine(_updateGrid);
            ConnToNearbyPairs.Remove(OwnerId);
        }
        public void InitHashGridComponent(HashGridCondition condition)
        {           
            //Network Object is null when component is added during runtime
            //This will grab the network object component
            _networkObject = GetComponent<NetworkObject>();
            _ownerId = _networkObject.OwnerId;
            //Setup update frequency and search settings then start the UpdateGrid method
            _cellSearchDistance = condition.GetCellSearchDistance;
            _updateFrequency = condition.GetUpdateFrequency;
            _updateGrid = StartCoroutine(UpdateGrid());
            //If the HashGrid is created then initialize it
            if (StaticHashGrid != null) return;
            StaticHashGrid = new HashGrid(condition.GetCellSize);
        }
        IEnumerator UpdateGrid()
        {
            //Wait for the HashGrid to be set.
            while (StaticHashGrid == null)
                yield return null;

            while (true)
            {
                yield return new WaitForSeconds(_updateFrequency);
                s_UpdateGrid.Begin();
                //Update the hashgrid with current position. Then update which cell you are in based on position.
                if (StaticHashGrid.UpdateGridWithPosition(_networkObject, transform.position, _currentCell, out var cacheCell))
                    _currentCell.CopyFrom(cacheCell);

                //Get a list of all nearby networkobject
                StaticHashGrid.GetNearbyNobs(_currentCell, _cellSearchDistance, out List<int> nearbyNobs);
                
                //If Dictonary doesn't contain our connection id then add it with the nearby nobs
                if (!ConnToNearbyPairs.ContainsKey(_ownerId))
                    ConnToNearbyPairs.Add(_ownerId, new List<int>(nearbyNobs));
                else
                {                    
                    foreach (var nob in nearbyNobs)
                        if(!ConnToNearbyPairs[_ownerId].Contains(nob))
                            ConnToNearbyPairs[_ownerId].Add(nob);
                    //Create an int array on the stack for no GC
                    //Set Size to list size + 1, The first int in the array will indicate how many nobs will be removed for next loop
                    Span<int> remove = stackalloc int[ConnToNearbyPairs[_ownerId].Count + 1];
                    remove[0] = 1;
                    //Add which nobs need to be removed from list to stack array
                    foreach (var nob in ConnToNearbyPairs[_ownerId])
                        if (!nearbyNobs.Contains(nob))
                        {
                            remove[remove[0]] = nob;
                            remove[0]++;
                        }
                    //Remove nobs from list
                    for(int i = 1; i < remove[0]; i++)
                        ConnToNearbyPairs[_ownerId].Remove(remove[i]);
                }
                nearbyNobs.Clear();
                s_UpdateGrid.End();
            }            
        }        
    }
}
