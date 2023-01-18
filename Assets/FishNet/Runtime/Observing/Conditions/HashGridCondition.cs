using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine;
using MMO;

namespace FishNet.Component.Observing
{    
    public class GridCell : IEquatable<GridCell>
    {
        //Public Properties
        //*****************
        public int X { get; private set; }
        public int Z { get; private set; }

        //Constructors
        //************
        public GridCell()
        {
            X = 0;
            Z = 0;
        }
        public GridCell(int x, int z)
        {
            X = x;
            Z = z;
        }
        public GridCell(GridCell cell)
        {
            this.X = cell.X;
            this.Z = cell.Z;
        }

        //Public Methods
        //**************
        public void Set(int x, int z)
        {
            X = x;
            Z = z; 
        }
        public void CopyFrom(GridCell cell)
        {
            Set(cell.X, cell.Z);
        }
        public void CopyTo(GridCell cell)
        {
            cell.Set(X, Z);
        }
        public GridCell Clone()
        {
            return new GridCell(X, Z);
        }

        //Class Overrides
        //***************
        public override string ToString()
        {
            return $"({X}, {Z})";
        }
        public override bool Equals(object obj)
        {
            if(obj is GridCell)
                return Equals((GridCell)obj);
            return false;
        }
        public bool Equals(GridCell other)
        {
            return other.X == X && other.Z == Z;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Z);
        }
        public static bool operator== (GridCell cell1, GridCell cell2)
        {
            return cell1.Equals(cell2);
        }
        public static bool operator!= (GridCell cell1, GridCell cell2)
        {
            return !(cell1.Equals(cell2));
        }        
    }
    public class HashGrid
    {
        //Size of grid cells
        private static int _gridCellSize = 0;
        
        //This is the hash grid that contains a cell loaction that points to a list of network objects
        public Dictionary<GridCell, List<int>> Grid = new Dictionary<GridCell, List<int>>(256);
        
        //Public Getters & Setters
        //************************
        //Sets the size of the grid cells. The size cannot be less then 1
        public int SetCellSize { set => _gridCellSize = Mathf.Max(value, 1); }
        public int GetCellSize => _gridCellSize;

        //Cache data
        private List<int>[] _cacheList = new List<int>[10];       
        private GridCell[] _cacheCell = new GridCell[10];
        //Constructors
        private void BaseConstructor()
        {
            for (int i = 0; i < _cacheCell.Length; i++)
                _cacheCell[i] = new GridCell();
            for (int i = 0; i < _cacheList.Length; i++)
                _cacheList[i] = new List<int>(32);
        }
        public HashGrid()
        {
            BaseConstructor();
        }
        public HashGrid(int cellSize)
        {
            BaseConstructor();
            _gridCellSize = cellSize;
        }        

        //Public Methods
        public bool UpdateGridWithPosition(NetworkObject nob, Vector3 pos, GridCell currectCell, out GridCell outCell)
        {
            //Calculate the cell for the nobs current position
            _cacheCell[0].Set((int)Mathf.Round(pos.x / _gridCellSize), (int)Mathf.Round(pos.z / _gridCellSize));
            outCell = _cacheCell[0];
            //If the grid cell doesn't exist, add it.
            if (!Grid.ContainsKey(outCell)) Grid.Add(new GridCell(outCell), new List<int>(64));
            
            //If the nob is in the list the exit early, since it is in the same cell            
            if (Grid[outCell].Contains(nob.OwnerId))
            {
                return false;
            }

            //Otherwise 
            //Remove from the old cell
            if (Grid.TryGetValue(currectCell, out var list))
            {
                list.Remove(nob.OwnerId);
            }

            //Add it to the new cell
            Grid[outCell].Add(nob.OwnerId);
            return true;
        }
        
        //Returns an array of all nearby Nobs to a given grid cell by searchDistance
        public bool GetNearbyNobs(GridCell cell, int searchDistance, out List<int> list)
        {
            _cacheList[1].Clear();
            //This will search through all grid cells that exist
            //from [x-searchDistance, z-searchDistance] to [x+searchDistance, z+searchDistance]
            for (int x = cell.X - searchDistance; x <= cell.X + searchDistance; x++)
            {
                for (int z = cell.Z - searchDistance; z <= cell.Z + searchDistance; z++)
                {
                    _cacheCell[1].Set(x, z);
                    if (!Grid.TryGetValue(_cacheCell[1], out var cellList)) continue;
                    _cacheList[1].AddRange(cellList);
                }
            }
            list = _cacheList[1];
            return true;
        }
    }

    /// <summary>
    /// When this observer condition is placed on an object, a client must be within a specified grid distance to view the object.
    /// </summary>
    [CreateAssetMenu(menuName = "FishNet/Observers/Hash Grid Condition", fileName = "New Hash Grid Condition")]
    public class HashGridCondition : ObserverCondition
    {
        static readonly ProfilerMarker s_ConditionMet = new ProfilerMarker("0 - Condition Met");
        //Private Fields
        //**************
        [Tooltip("How often this condition may change for a connection. This prevents objects from appearing and disappearing rapidly. A value of 0f will cause the object the update quickly as possible while any other value will be used as a delay.")]
        [SerializeField, Range(0f, 60f)]
        private float _updateFrequency;
        [Tooltip("This defines the size of your grid cell.")]
        [SerializeField, Min(1)]
        private int _cellSize = 12;
        [Tooltip("The number of cells a client must be within this object to see it.")]
        [SerializeField]
        private int _cellSearchDistance = 10;
        /// <summary>
        /// Tracks when connections may be updated for this object.
        /// </summary>
        private Dictionary<NetworkConnection, float> _timedUpdates = new Dictionary<NetworkConnection, float>();

        //Public Getters & Setters
        //************************
        /// <summary>
        /// How often this condition may change for a connection. This prevents objects from appearing and disappearing rapidly. A value of 0f will cause the object the update quickly as possible while any other value will be used as a delay.
        /// </summary>
        public float GetUpdateFrequency => _updateFrequency;
        /// <summary>
        /// The number of cells a client must be within this object to see it.<para/>
        /// A search distance of one looks like:<para/>
        /// [ 1, -1] [ 1, 0] [ 1, 1]<para/>
        /// [ 0, -1] [ 0, 0] [ 0, 1]<para/>
        /// [-1,-1] [-1,0] [-1, 1]<para/>
        /// </summary>
        public int GetCellSerachDistance => _cellSearchDistance;
        /// <summary>
        /// Additional number of cells a client must be until this object is hidden. For example, if cellSearchDistance is 10 and hideDistancePadding is 2 the client must be 12 grid cells away before this object is hidden again. This can be useful for keeping objects from regularly appearing and disappearing.
        /// </summary>
        public int GetCellSearchDistance => _cellSearchDistance;
        /// <summary>
        /// Get the size of the grid cell.
        /// </summary>
        public int GetCellSize => _cellSize;

        public override void InitializeOnce(NetworkObject networkObject)
        {
            base.InitializeOnce(networkObject);
            //Verify if the nob has a HashGridComponent if not add it
            var hashGridComponent = networkObject.gameObject.GetComponent<HashGridComponent>();
            if(hashGridComponent == null)
                hashGridComponent = networkObject.gameObject.AddComponent<HashGridComponent>();
            //Initialize the HashGridComponent
            hashGridComponent.InitHashGridComponent(this);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            s_ConditionMet.Begin();
            if (_updateFrequency > 0f)
            {
                if (!_timedUpdates.TryGetValueIL2CPP(connection, out float nextAllowedUpdate))
                {
                    _timedUpdates[connection] = (Time.time + _updateFrequency);
                }
                else
                {
                    //Not enough time to process again.
                    if (Time.time < nextAllowedUpdate)
                    {
                        notProcessed = true;
                        //The return does not really matter since notProcessed is returned.
                        s_ConditionMet.End();
                        return false;
                    }
                    //Can process again.
                    else
                    {
                        _timedUpdates[connection] = (Time.time + _updateFrequency);
                    }
                }
            }
            notProcessed = false;

            //Null check, return false if the Dicionary isn't ready
            if (HashGridComponent.ConnToNearbyPairs == null)
            {
                s_ConditionMet.End();
                return false;
            }

            //Check the dictionary for the clientId and see if this Nob is in it
            if (HashGridComponent.ConnToNearbyPairs.TryGetValue(connection.ClientId, out var list))
            {
                if (list.Contains(NetworkObject.OwnerId))
                {
                    s_ConditionMet.End();
                    return true;
                }
            }
            s_ConditionMet.End();
            return false;
        }
        public void ConditionConstructor(int searchDistance, int cellSize, float updateFrequency)
        {
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
            copy.ConditionConstructor(_cellSearchDistance, _cellSize, _updateFrequency);
            return copy;
        }
    }
}
