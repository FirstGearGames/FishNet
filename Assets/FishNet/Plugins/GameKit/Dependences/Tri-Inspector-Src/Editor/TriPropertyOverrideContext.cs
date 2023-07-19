using System;
using UnityEngine;

namespace TriInspector
{
    public abstract class TriPropertyOverrideContext
    {
        private static TriPropertyOverrideContext Override { get; set; }
        public static TriPropertyOverrideContext Current { get; private set; }

        public abstract bool TryGetDisplayName(TriProperty property, out GUIContent displayName);

        public static EnterPropertyScope BeginProperty()
        {
            return new EnterPropertyScope().Init();
        }

        public static OverrideScope BeginOverride(TriPropertyOverrideContext overrideContext)
        {
            return new OverrideScope(overrideContext);
        }

        public struct EnterPropertyScope : IDisposable
        {
            private TriPropertyOverrideContext _previousContext;

            public EnterPropertyScope Init()
            {
                _previousContext = Current;
                Current = Override;
                return this;
            }

            public void Dispose()
            {
                Override = Current;
                Current = _previousContext;
            }
        }

        public readonly struct OverrideScope : IDisposable
        {
            public OverrideScope(TriPropertyOverrideContext context)
            {
                if (Override != null)
                {
                    Debug.LogError($"TriPropertyContext already overriden with {Override.GetType()}");
                }

                Override = context;
            }

            public void Dispose()
            {
                Override = null;
            }
        }
    }
}