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
        public readonly int X;
        public readonly int Z;

        public GridCell(int x, int z)
        {
            X = x;
            Z = z;
        }
    }
    public class HashGrid
    {
        //Size of grid cells
        private int _gridCellSize = 5;
        
        //This is the hash grid that contains a cell loaction that points to a list of network objects
        public readonly Dictionary<GridCell, List<NetworkObject>> Grid = new Dictionary<GridCell, List<NetworkObject>>();
        
        //Public Getters & Setters
        //************************
        //Sets the size of the grid cells. The size cannot be less then 1
        public int SetCellSize { set => _gridCellSize = Mathf.Max(value, 1); }
        public int GetCellSize => _gridCellSize;
        //Public Methods
        public GridCell UpdateGridWithPosition(NetworkObject nob, Vector3 pos, GridCell currectCell)
        {
            //Calculate the cell for the nobs current position
            GridCell newCell = new GridCell((int)Mathf.Round(pos.x / _gridCellSize), (int)Mathf.Round(pos.z / _gridCellSize));
            
            //If the grid cell doesn't exist, add it.
            if(!Grid.ContainsKey(newCell)) Grid.Add(newCell, new List<NetworkObject>());

            //If the nob is in the list the exit early, since it is in the same cell
            if (Grid[newCell].Contains(nob)) return newCell;
            //Otherwise add it to the new cell
            else Grid[newCell].Add(nob);

            //Remove from the old cell
            if(Grid.TryGetValue(currectCell, out List<NetworkObject> list))
                list.Remove(nob);

            return newCell;
        }
        //Returns an array of all nearby Nobs to a given grid cell by searchDistance
        public NetworkObject[] GetNearbyNobs(GridCell cell, int searchDistance)
        {
            List<NetworkObject> nobs = new List< NetworkObject >();
            //This will search through all grid cells that exist
            //from [x-searchDistance, z-searchDistance] to [x+searchDistance, z+searchDistance]
            for (int x = cell.X - searchDistance; x <= cell.X + searchDistance; x++)
            {
                for (int z = cell.Z - searchDistance; z <= cell.Z + searchDistance; z++)
                {
                    if (Grid.TryGetValue(new GridCell(x, z), out var list)) nobs.AddRange(list);
                }
            }
            return nobs.ToArray();
        }
    }

    /// <summary>
    /// When this observer condition is placed on an object, a client must be within a specified grid distance to view the object.
    /// </summary>
    [CreateAssetMenu(menuName = "FishNet/Observers/Hash Grid Condition", fileName = "New Hash Grid Condition")]
    public class HashGridCondition : ObserverCondition
    {
        /// <summary>
        /// Singleton instance of the HashGrid
        /// </summary>
        public static HashGrid StaticHashGrid { get; private set; }

        //Private Fields
        //**************
        [Tooltip("How often this condition may change for a connection. This prevents objects from appearing and disappearing rapidly. A value of 0f will cause the object the update quickly as possible while any other value will be used as a delay.")]
        [SerializeField, Range(0f, 60f)]
        private float _updateFrequency;
        /// <summary>
        /// This defines the size of your grid cell.<para/>
        /// This effects the search distance.
        /// </summary>
        [Tooltip("This defines the size of your grid cell.")]
        [SerializeField, Min(1)]
        private int _cellSize = 5;
        /// <summary>
        /// The number of cells a client must be within this object to see it.<para/>
        /// A search distance of one looks like:<para/>
        /// [ 1, -1] [ 1, 0] [ 1, 1]<para/>
        /// [ 0, -1] [ 0, 0] [ 0, 1]<para/>
        /// [-1,-1] [-1,0] [-1, 1]<para/>
        /// </summary>
        [SerializeField]
        private int _cellSearchDistance = 25;
        /// <summary>
        /// Additional number of cells a client must be until this object is hidden. For example, if cellSearchDistance is 10 and hideDistancePadding is 2 the client must be 12 grid cells away before this object is hidden again. This can be useful for keeping objects from regularly appearing and disappearing.
        /// </summary>
        [Tooltip("Additional number of cells a client must be until this object is hidden. For example, if cellSearchDistance is 10 and hideDistancePadding is 2 the client must be 12 grid cells away before this object is hidden again. This can be useful for keeping objects from regularly appearing and disappearing.")]
        [Range(0f, 1f)]
        [SerializeField]
        private int _hideDistancePadding = 2;
        /// <summary>
        /// Tracks when connections may be updated for this object.
        /// </summary>
        private Dictionary<NetworkConnection, float> _timedUpdates = new Dictionary<NetworkConnection, float>();
        /// <summary>
        /// Keeps track of the Network Objects current cell.
        /// </summary>
        private GridCell _currectCell = new GridCell();

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
        public int GetHideDistancePadding => _hideDistancePadding;

        private void Awake()
        {
            //Setup the singleton instance of the Hash Grid, only if not setup.
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
                    //Not enough time to process again.
                    if (currentTime < nextAllowedUpdate)
                    {
                        notProcessed = true;
                        //The return does not really matter since notProcessed is returned.
                        return false;
                    }
                    //Can process again.
                    else
                    {
                        _timedUpdates[connection] = (currentTime + _updateFrequency);
                    }
                }
            }
            notProcessed = false;
            //Update the Hash Grid with the current position of the network object, returns the update cell.
            _currectCell = StaticHashGrid.UpdateGridWithPosition(NetworkObject, NetworkObject.transform.position, _currectCell);

            //If visible add padding to search distance. Otherwise use regular search distance.
            int serachDistance = currentlyAdded ? _cellSearchDistance + _hideDistancePadding : _cellSearchDistance;

            //Returns a list of network objects in all of the cells, within the search distance.
            var nobs = StaticHashGrid.GetNearbyNobs(_currectCell, serachDistance);

            //If a network object is in the grid return true.
            foreach (NetworkObject nob in nobs)
                foreach(var obj in connection.Objects)
                    if(nob == obj) return true;

            //Network object is not in the grid.
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
