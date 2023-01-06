using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Utility.Extension;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MMO
{
    public struct GridCell
    {
        public readonly bool Set;
        public readonly int X;
        public readonly int Z;

        public GridCell(int x, int z, bool set = true)
        {
            X = x;
            Z = z;
            Set = set;
        }
    }
    public class HashGrid
    {
        private int _gridCellSize = 5;

        public Dictionary<GridCell, List<NetworkObject>> Grid = new Dictionary<GridCell, List<NetworkObject>>();
        
        //Public Getters & Setters
        public int SetCellSize { set => _gridCellSize = value; }
        public int GetCellSize => _gridCellSize;
        //Public Methods
        public GridCell UpdateGrid(NetworkObject nob, Vector3 pos, GridCell currectCell)
        {
            int x = Mathf.Round(pos.x / _gridCellSize).ToInt();
            int z = Mathf.Round(pos.z / _gridCellSize).ToInt();
            if(currectCell.X == x && currectCell.Z == z)
            {
                return new GridCell(x, z);
            }
            GridCell newCell = new GridCell(x, z);
            if (currectCell.Set) Grid[currectCell].Remove(nob);
            if(!Grid.ContainsKey(newCell)) Grid.Add(newCell, new List<NetworkObject>());
            Grid[newCell].Add(nob);
            return newCell;
        }
        public NetworkObject[] GetNearbyEntities(GridCell cell, int searchDistance)
        {
            List<NetworkObject> nobs = new List< NetworkObject >();
            for(int x = cell.X - searchDistance; x <= cell.X + searchDistance; x++)
            {
                for (int z = cell.Z - searchDistance; z <= cell.Z + searchDistance; z++)
                {
                    if (Grid.TryGetValue(new GridCell(x, z), out var list)) nobs.AddRange(list);
                }
            }
            return nobs.ToArray();
        }
    }

    [CreateAssetMenu(menuName = "FishNet/Observers/Hash Grid Condition", fileName = "New Hash Grid Condition")]
    public class HashGridCondition : ObserverCondition
    {
        public static HashGrid StaticHashGrid { get; private set; }
        //Serialized Fields
        [SerializeField]
        private float _updateFrequency;
        [SerializeField]
        private int _cellSize = 5;
        [SerializeField]
        private int _cellSearchDistance = 25;
        [SerializeField]
        private int _hideDistancePadding = 2;

        private Dictionary<NetworkConnection, float> _timedUpdates = new Dictionary<NetworkConnection, float>();
        private GridCell _currectCell = new GridCell(0, 0, false);

        //Public Getters & Setters
        public float GetUpdateFrequency => _updateFrequency;
        public int GetCellSerachDistance => _cellSearchDistance;
        public int GetHideDistancePadding => _hideDistancePadding;

        private void Awake()
        {
            if (StaticHashGrid != null) return;
            StaticHashGrid = new HashGrid();
            StaticHashGrid.SetCellSize = _cellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            if (_updateFrequency > 0f)
            {
                float nextAllowedUpdate;
                float currentTime = Time.time;
                if (!_timedUpdates.TryGetValueIL2CPP(connection, out nextAllowedUpdate))
                {
                    _timedUpdates[connection] = (currentTime + _updateFrequency);
                }
                else
                {
                    if (currentTime < nextAllowedUpdate)
                    {
                        notProcessed = true;
                        return false;
                    }
                    else
                    {
                        _timedUpdates[connection] = (currentTime + _updateFrequency);
                    }
                }
            }
            notProcessed = false;
            _currectCell = StaticHashGrid.UpdateGrid(NetworkObject, NetworkObject.transform.position, _currectCell);
            int serachDistance = currentlyAdded ? _cellSearchDistance + _hideDistancePadding :_cellSearchDistance;
            var nobs = StaticHashGrid.GetNearbyEntities(_currectCell, serachDistance);

            foreach (NetworkObject nob in nobs)
                foreach(var obj in connection.Objects)
                    if(nob == obj) return true;

            return false;
        }
        public void ConditionConstructor(int searchDistance, int hidePadding, int cellSize, float updateFrequency)
        {
            _hideDistancePadding = hidePadding;
            _cellSearchDistance = searchDistance;
            _cellSize = cellSize;
            _updateFrequency = updateFrequency;
        }
        public override bool Timed()
        {
            return true;
        }
        public override ObserverCondition Clone()
        {
            HashGridCondition copy = ScriptableObject.CreateInstance<HashGridCondition>();
            copy.ConditionConstructor(_cellSearchDistance, _hideDistancePadding, _cellSize, _updateFrequency);
            return copy;
        }
    }
}
