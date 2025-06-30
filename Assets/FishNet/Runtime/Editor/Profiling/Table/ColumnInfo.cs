using System;
using Fishnet.NetworkProfiler.ModuleGUI.Messages;
using FishNet.Utility.Performance.Profiling;

namespace Fishnet.NetworkProfiler.ModuleGUI.UITable
{
    internal class ColumnInfo
    {
        public string Header { get; private set; }
        public int Width { get; private set; }

        public bool AllowSort { get; private set; }
        public Func<Group, Group, int> SortGroup { get; private set; }
        public Func<PacketInfo, PacketInfo, int> SortMessages { get; private set; }

        public Func<PacketInfo, string> TextGetter { get; private set; }

        public bool HasToolTip { get; private set; }
        public Func<PacketInfo, string> ToolTipGetter { get; private set; }

        public ColumnInfo(string header, int width, Func<PacketInfo, string> textGetter)
        {
            Header = header;
            Width = width;
            TextGetter = textGetter;
        }

        /// <summary>
        /// Enables sorting for column. If sort functions are null they will use default sort from <see cref="GroupSorter"/>
        /// </summary>
        /// <param name="sortGroup"></param>
        /// <param name="sortMessages"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddSort(Func<Group, Group, int> sortGroup, Func<PacketInfo, PacketInfo, int> sortMessages)
        {
            AllowSort = true;
            SortGroup = sortGroup ?? GroupSorter.DefaultGroupSort;
            SortMessages = sortMessages ?? GroupSorter.DefaultMessageSort;
        }

        /// <summary>
        /// Enables sorting for column. If sort functions are null they will use default sort from <see cref="GroupSorter"/>
        /// <para>Uses member getting to sort via that member</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="groupGetter"></param>
        /// <param name="messageGetter"></param>
        public void AddSort<T>(Func<Group, T> groupGetter, Func<PacketInfo, T> messageGetter) where T : IComparable<T>
        {
            Func<Group, Group, int> sortGroup = groupGetter != null
                ? (x, y) => GroupSorter.Compare(x, y, groupGetter)
                : null;

            Func<PacketInfo, PacketInfo, int> sortMessages = messageGetter != null
                ? (x, y) => GroupSorter.Compare(x, y, messageGetter)
                : null;

            AddSort(sortGroup, sortMessages);
        }

        public void AddToolTip(Func<PacketInfo, string> getter)
        {
            HasToolTip = true;
            ToolTipGetter = getter;
        }
    }
}
