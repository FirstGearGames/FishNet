using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TriInspector.Utilities;

namespace TriInspector.Elements
{
    public abstract class TriPropertyCollectionBaseElement : TriElement
    {
        private List<DeclareGroupBaseAttribute> _declarations = new List<DeclareGroupBaseAttribute>();

        private Dictionary<string, TriPropertyCollectionBaseElement> _groups;

        [PublicAPI]
        public void DeclareGroups([CanBeNull] Type type)
        {
            if (type == null)
            {
                return;
            }

            foreach (var attribute in TriReflectionUtilities.GetAttributesCached(type))
            {
                if (attribute is DeclareGroupBaseAttribute declareAttribute)
                {
                    _declarations.Add(declareAttribute);
                }
            }
        }

        [PublicAPI]
        public void AddProperty(TriProperty property)
        {
            AddProperty(property, default, out _);
        }

        [PublicAPI]
        public void AddProperty(TriProperty property, TriPropertyElement.Props props, out string group)
        {
            var propertyElement = new TriPropertyElement(property, props);

            if (property.TryGetAttribute(out GroupAttribute groupAttribute))
            {
                IEnumerable<string> path = groupAttribute.Path.Split('/');

                var remaining = path.GetEnumerator();
                if (remaining.MoveNext())
                {
                    group = remaining.Current;
                    AddGroupedChild(propertyElement, property, remaining.Current, remaining.Current, remaining);
                }
                else
                {
                    group = null;
                    AddPropertyChild(propertyElement, property);
                }
            }
            else
            {
                group = null;
                AddPropertyChild(propertyElement, property);
            }
        }

        private void AddGroupedChild(TriElement child, TriProperty property, string currentPath, string currentName,
            IEnumerator<string> remainingPath)
        {
            if (_groups == null)
            {
                _groups = new Dictionary<string, TriPropertyCollectionBaseElement>();
            }

            var groupElement = CreateSubGroup(property, currentPath, currentName);

            if (remainingPath.MoveNext())
            {
                var nextPath = currentPath + "/" + remainingPath.Current;
                var nextName = remainingPath.Current;

                groupElement.AddGroupedChild(child, property, nextPath, nextName, remainingPath);
            }
            else
            {
                groupElement.AddPropertyChild(child, property);
            }
        }

        private TriPropertyCollectionBaseElement CreateSubGroup(TriProperty property,
            string groupPath, string groupName)
        {
            if (!_groups.TryGetValue(groupName, out var groupElement))
            {
                var declaration = _declarations.FirstOrDefault(it => it.Path == groupPath);

                if (declaration != null)
                {
                    groupElement = TriDrawersUtilities.TryCreateGroupElementFor(declaration);
                }

                if (groupElement == null)
                {
                    groupElement = new DefaultGroupElement();
                }

                groupElement._declarations = _declarations;

                _groups.Add(groupName, groupElement);

                AddPropertyChild(groupElement, property);
            }

            return groupElement;
        }

        protected virtual void AddPropertyChild(TriElement element, TriProperty property)
        {
            AddChild(element);
        }

        private class DefaultGroupElement : TriPropertyCollectionBaseElement
        {
        }
    }
}