using System.Collections.Generic;
using TriInspector.Elements;
using UnityEditor;

namespace TriInspector
{
    internal class ValidatorsDrawer : TriCustomDrawer
    {
        public override TriElement CreateElementInternal(TriProperty property, TriElement next)
        {
            if (!property.HasValidators)
            {
                return next;
            }

            var element = new TriElement();
            element.AddChild(new TriPropertyValidationResultElement(property));
            element.AddChild(next);
            return element;
        }

        public class TriPropertyValidationResultElement : TriElement
        {
            private readonly TriProperty _property;
            private IReadOnlyList<TriValidationResult> _validationResults;

            public TriPropertyValidationResultElement(TriProperty property)
            {
                _property = property;
            }

            public override float GetHeight(float width)
            {
                if (ChildrenCount == 0)
                {
                    return -EditorGUIUtility.standardVerticalSpacing;
                }

                return base.GetHeight(width);
            }

            public override bool Update()
            {
                var dirty = base.Update();

                dirty |= GenerateValidationResults();

                return dirty;
            }

            private bool GenerateValidationResults()
            {
                if (ReferenceEquals(_property.ValidationResults, _validationResults))
                {
                    return false;
                }

                _validationResults = _property.ValidationResults;

                RemoveAllChildren();

                foreach (var result in _validationResults)
                {
                    AddChild(new TriInfoBoxElement(result.Message, result.MessageType));
                }

                return true;
            }
        }
    }
}