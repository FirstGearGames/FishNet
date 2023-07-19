using JetBrains.Annotations;
using UnityEngine;

namespace TriInspector
{
    public abstract class TriValueDrawer : TriCustomDrawer
    {
    }

    public abstract class TriValueDrawer<TValue> : TriValueDrawer
    {
        public sealed override TriElement CreateElementInternal(TriProperty property, TriElement next)
        {
            return CreateElement(new TriValue<TValue>(property), next);
        }

        [PublicAPI]
        public virtual TriElement CreateElement(TriValue<TValue> propertyValue, TriElement next)
        {
            return new DefaultValueDrawerElement<TValue>(this, propertyValue, next);
        }

        [PublicAPI]
        public virtual float GetHeight(float width, TriValue<TValue> propertyValue, TriElement next)
        {
            return next.GetHeight(width);
        }

        [PublicAPI]
        public virtual void OnGUI(Rect position, TriValue<TValue> propertyValue, TriElement next)
        {
            next.OnGUI(position);
        }

        internal class DefaultValueDrawerElement<T> : TriElement
        {
            private readonly TriValueDrawer<T> _drawer;
            private readonly TriElement _next;
            private readonly TriValue<T> _propertyValue;

            public DefaultValueDrawerElement(TriValueDrawer<T> drawer, TriValue<T> propertyValue, TriElement next)
            {
                _drawer = drawer;
                _propertyValue = propertyValue;
                _next = next;

                AddChild(next);
            }

            public override float GetHeight(float width)
            {
                return _drawer.GetHeight(width, _propertyValue, _next);
            }

            public override void OnGUI(Rect position)
            {
                _drawer.OnGUI(position, _propertyValue, _next);
            }
        }
    }
}