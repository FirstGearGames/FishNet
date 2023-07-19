using System;
using JetBrains.Annotations;
using UnityEngine;

namespace TriInspector
{
    public abstract class TriAttributeDrawer : TriCustomDrawer
    {
        internal Attribute RawAttribute { get; set; }
    }

    public abstract class TriAttributeDrawer<TAttribute> : TriAttributeDrawer
        where TAttribute : Attribute
    {
        [PublicAPI]
        public TAttribute Attribute => (TAttribute) RawAttribute;

        public sealed override TriElement CreateElementInternal(TriProperty property, TriElement next)
        {
            return CreateElement(property, next);
        }

        [PublicAPI]
        public virtual TriElement CreateElement(TriProperty property, TriElement next)
        {
            return new DefaultAttributeDrawerElement(this, property, next);
        }

        [PublicAPI]
        public virtual float GetHeight(float width, TriProperty property, TriElement next)
        {
            return next.GetHeight(width);
        }

        [PublicAPI]
        public virtual void OnGUI(Rect position, TriProperty property, TriElement next)
        {
            next.OnGUI(position);
        }

        internal class DefaultAttributeDrawerElement : TriElement
        {
            private readonly TriAttributeDrawer<TAttribute> _drawer;
            private readonly TriElement _next;
            private readonly TriProperty _property;

            public DefaultAttributeDrawerElement(TriAttributeDrawer<TAttribute> drawer, TriProperty property,
                TriElement next)
            {
                _drawer = drawer;
                _property = property;
                _next = next;

                AddChild(next);
            }

            public override float GetHeight(float width)
            {
                return _drawer.GetHeight(width, _property, _next);
            }

            public override void OnGUI(Rect position)
            {
                _drawer.OnGUI(position, _property, _next);
            }
        }
    }
}