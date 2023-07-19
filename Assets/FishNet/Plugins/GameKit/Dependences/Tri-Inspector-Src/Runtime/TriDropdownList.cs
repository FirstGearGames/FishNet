using System.Collections.Generic;

namespace TriInspector
{
    public class TriDropdownList<T> : List<TriDropdownItem<T>>
    {
        public void Add(string text, T value)
        {
            Add(new TriDropdownItem<T> {Text = text, Value = value,});
        }
    }

    public interface ITriDropdownItem
    {
        string Text { get; }
        object Value { get; }
    }

    public struct TriDropdownItem : ITriDropdownItem
    {
        public string Text { get; set; }
        public object Value { get; set; }
    }

    public struct TriDropdownItem<T> : ITriDropdownItem
    {
        public string Text;
        public T Value;

        string ITriDropdownItem.Text => Text;
        object ITriDropdownItem.Value => Value;
    }
}