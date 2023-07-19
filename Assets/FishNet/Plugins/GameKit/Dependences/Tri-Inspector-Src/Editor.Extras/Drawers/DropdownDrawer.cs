using System.Collections.Generic;
using System.Linq;
using TriInspector;
using TriInspector.Drawers;
using TriInspector.Elements;
using TriInspector.Resolvers;

[assembly: RegisterTriAttributeDrawer(typeof(DropdownDrawer<>), TriDrawerOrder.Decorator)]

namespace TriInspector.Drawers
{
    public class DropdownDrawer<T> : TriAttributeDrawer<DropdownAttribute>
    {
        private ValueResolver<IEnumerable<TriDropdownItem<T>>> _itemsResolver;
        private ValueResolver<IEnumerable<T>> _valuesResolver;

        public override TriExtensionInitializationResult Initialize(TriPropertyDefinition propertyDefinition)
        {
            _valuesResolver = ValueResolver.Resolve<IEnumerable<T>>(propertyDefinition, Attribute.Values);

            if (_valuesResolver.TryGetErrorString(out _))
            {
                _itemsResolver =
                    ValueResolver.Resolve<IEnumerable<TriDropdownItem<T>>>(propertyDefinition, Attribute.Values);

                if (_itemsResolver.TryGetErrorString(out var itemResolverError))
                {
                    return itemResolverError;
                }
            }

            return TriExtensionInitializationResult.Ok;
        }

        public override TriElement CreateElement(TriProperty property, TriElement next)
        {
            return new TriDropdownElement(property, GetDropdownItems);
        }

        private IEnumerable<ITriDropdownItem> GetDropdownItems(TriProperty property)
        {
            if (_valuesResolver != null)
            {
                var values = _valuesResolver.GetValue(property, Enumerable.Empty<T>());

                foreach (var value in values)
                {
                    yield return new TriDropdownItem {Text = $"{value}", Value = value,};
                }
            }

            if (_itemsResolver != null)
            {
                var values = _itemsResolver.GetValue(property, Enumerable.Empty<TriDropdownItem<T>>());

                foreach (var value in values)
                {
                    yield return value;
                }
            }
        }
    }
}