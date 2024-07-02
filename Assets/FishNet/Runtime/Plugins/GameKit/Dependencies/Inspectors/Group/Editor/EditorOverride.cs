///* Commented out due to Unity 2022 bug where
// * you cannot create a custom editor override
// * which won't break users custom VisualElements. */

//#if UNITY_EDITOR
////  Project : UNITY FOLDOUT
//// Contacts : Pix - ask@pixeye.games
//// https://github.com/PixeyeHQ/InspectorFoldoutGroup
//// MIT license https://github.com/PixeyeHQ/InspectorFoldoutGroup/blob/master/LICENSE

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using UnityEditor;
//using UnityEditor.UIElements;
//using UnityEngine;
//using UnityEngine.UIElements;
//using Object = UnityEngine.Object;


//namespace GameKit.Dependencies.Inspectors
//{
//    [CustomEditor(typeof(Object), true, isFallback = true)]
//    [CanEditMultipleObjects]
//    public class EditorOverride : Editor
//    {

//        public override VisualElement CreateInspectorGUI()
//        {
//            var container = new VisualElement();

//            var iterator = serializedObject.GetIterator();
//            if (iterator.NextVisible(true))
//            {
//                do
//                {
//                    var propertyField = new PropertyField(iterator.Copy()) { name = "PropertyField:" + iterator.propertyPath };

//                    if (iterator.propertyPath == "m_Script" && serializedObject.targetObject != null)
//                        propertyField.SetEnabled(value: false);

//                    container.Add(propertyField);
//                }
//                while (iterator.NextVisible(false));
//            }

//            return container;
//        }


//        //===============================//
//        // Members
//        //===============================//

//        Dictionary<string, CacheFoldProp> cacheFolds = new Dictionary<string, CacheFoldProp>();
//        List<SerializedProperty> props = new List<SerializedProperty>();
//        List<MethodInfo> methods = new List<MethodInfo>();
//        bool initialized;


//        //===============================//
//        // Logic
//        //===============================//
//        void OnEnable()
//        {
//            initialized = false;
//        }


//        void OnDisable()
//        {
//            //if (Application.wantsToQuit)
//            //if (applicationIsQuitting) return;
//            //	if (Toolbox.isQuittingOrChangingScene()) return;
//            if (target != null)
//                foreach (var c in cacheFolds)
//                {
//                    EditorPrefs.SetBool(string.Format($"{c.Value.atr.name}{c.Value.props[0].name}{target.GetInstanceID()}"), c.Value.expanded);
//                    c.Value.Dispose();
//                }
//        }


//        public override bool RequiresConstantRepaint()
//        {
//            return EditorFramework.needToRepaint;
//        }

//        public override void OnInspectorGUI()
//        {
//            serializedObject.Update();


//            Setup();

//            if (props.Count == 0)
//            {
//                DrawDefaultInspector();
//                return;
//            }

//            Header();
//            Body();

//            serializedObject.ApplyModifiedProperties();

//            void Header()
//            {
//                using (new EditorGUI.DisabledScope("m_Script" == props[0].propertyPath))
//                {
//                    EditorGUILayout.Space();
//                    EditorGUILayout.PropertyField(props[0], true);
//                    EditorGUILayout.Space();
//                }
//            }

//            void Body()
//            {
//                foreach (var pair in cacheFolds)
//                {
//                    this.UseVerticalLayout(() => Foldout(pair.Value), StyleFramework.box);
//                    EditorGUI.indentLevel = 0;
//                }

//                EditorGUILayout.Space();

//                for (var i = 1; i < props.Count; i++)
//                {
//                    // if (props[i].isArray)
//                    // {
//                    // 	DrawPropertySortableArray(props[i]);
//                    // }
//                    // else
//                    // {
//                    EditorGUILayout.PropertyField(props[i], true);
//                    //}
//                }

//                EditorGUILayout.Space();

//                if (methods == null) return;
//                foreach (MethodInfo memberInfo in methods)
//                {
//                    this.UseButton(memberInfo);
//                }
//            }

//            void Foldout(CacheFoldProp cache)
//            {
//                cache.expanded = EditorGUILayout.Foldout(cache.expanded, cache.atr.name, true,
//                        StyleFramework.foldout);

//                if (cache.expanded)
//                {
//                    EditorGUI.indentLevel = 1;

//                    for (int i = 0; i < cache.props.Count; i++)
//                    {
//                        this.UseVerticalLayout(() => Child(i), StyleFramework.boxChild);
//                    }
//                }

//                void Child(int i)
//                {
//                    // if (cache.props[i].isArray)
//                    // {
//                    // 	DrawPropertySortableArray(cache.props[i]);
//                    // }
//                    // else
//                    // {
//                    EditorGUILayout.PropertyField(cache.props[i], new GUIContent(ObjectNames.NicifyVariableName(cache.props[i].name)), true);
//                    //}
//                }
//            }

//            void Setup()
//            {
//                EditorFramework.currentEvent = Event.current;
//                if (!initialized)
//                {
//                    //	SetupButtons();

//                    List<FieldInfo> objectFields;
//                    GroupAttribute prevFold = default;

//                    var length = EditorTypes.Get(target, out objectFields);

//                    for (var i = 0; i < length; i++)
//                    {
//                        #region FOLDERS

//                        var fold = Attribute.GetCustomAttribute(objectFields[i], typeof(GroupAttribute)) as GroupAttribute;
//                        CacheFoldProp c;
//                        if (fold == null)
//                        {
//                            if (prevFold != null && prevFold.foldEverything)
//                            {
//                                if (!cacheFolds.TryGetValue(prevFold.name, out c))
//                                {
//                                    cacheFolds.Add(prevFold.name, new CacheFoldProp { atr = prevFold, types = new HashSet<string> { objectFields[i].Name } });
//                                }
//                                else
//                                {
//                                    c.types.Add(objectFields[i].Name);
//                                }
//                            }

//                            continue;
//                        }

//                        prevFold = fold;

//                        if (!cacheFolds.TryGetValue(fold.name, out c))
//                        {
//                            var expanded = EditorPrefs.GetBool(string.Format($"{fold.name}{objectFields[i].Name}{target.GetInstanceID()}"), false);
//                            cacheFolds.Add(fold.name, new CacheFoldProp { atr = fold, types = new HashSet<string> { objectFields[i].Name }, expanded = expanded });
//                        }
//                        else c.types.Add(objectFields[i].Name);

//                        #endregion
//                    }

//                    var property = serializedObject.GetIterator();
//                    var next = property.NextVisible(true);
//                    if (next)
//                    {
//                        do
//                        {
//                            HandleFoldProp(property);
//                        } while (property.NextVisible(false));
//                    }

//                    initialized = true;
//                }
//            }

//            // void SetupButtons()
//            // {
//            // 	var members = GetButtonMembers(target);
//            //
//            // 	foreach (var memberInfo in members)
//            // 	{
//            // 		var method = memberInfo as MethodInfo;
//            // 		if (method == null)
//            // 		{
//            // 			continue;
//            // 		}
//            //
//            // 		if (method.GetParameters().Length > 0)
//            // 		{
//            // 			continue;
//            // 		}
//            //
//            // 		if (methods == null) methods = new List<MethodInfo>();
//            // 		methods.Add(method);
//            // 	}
//            // }
//        }

//        public void HandleFoldProp(SerializedProperty prop)
//        {
//            bool shouldBeFolded = false;

//            foreach (var pair in cacheFolds)
//            {
//                if (pair.Value.types.Contains(prop.name))
//                {
//                    var pr = prop.Copy();
//                    shouldBeFolded = true;
//                    pair.Value.props.Add(pr);

//                    break;
//                }
//            }

//            if (shouldBeFolded == false)
//            {
//                var pr = prop.Copy();
//                props.Add(pr);
//            }
//        }

//        // IEnumerable<MemberInfo> GetButtonMembers(object target)
//        // {
//        // 	return target.GetType()
//        // 			.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.NonPublic)
//        // 			.Where(CheckButtonAttribute);
//        // }

//        // bool CheckButtonAttribute(MemberInfo memberInfo)
//        // {
//        // 	return Attribute.IsDefined(memberInfo, typeof(ButtonAttribute));
//        // }

//        class CacheFoldProp
//        {
//            public HashSet<string> types = new HashSet<string>();
//            public List<SerializedProperty> props = new List<SerializedProperty>();
//            public GroupAttribute atr;
//            public bool expanded;

//            public void Dispose()
//            {
//                props.Clear();
//                types.Clear();
//                atr = null;
//            }
//        }
//    }

//    static class EditorUIHelper
//    {
//        public static void UseVerticalLayout(this Editor e, Action action, GUIStyle style)
//        {
//            EditorGUILayout.BeginVertical(style);
//            action();
//            EditorGUILayout.EndVertical();
//        }

//        public static void UseButton(this Editor e, MethodInfo m)
//        {
//            if (GUILayout.Button(m.Name))
//            {
//                m.Invoke(e.target, null);
//            }
//        }
//    }


//    static class StyleFramework
//    {
//        public static GUIStyle box;
//        public static GUIStyle boxChild;
//        public static GUIStyle foldout;
//        public static GUIStyle button;
//        public static GUIStyle text;

//        static StyleFramework()
//        {
//            bool pro = EditorGUIUtility.isProSkin;


//            var uiTex_in = UnityEngine.Resources.Load<Texture2D>("IN foldout focus-6510");
//            var uiTex_in_on = UnityEngine.Resources.Load<Texture2D>("IN foldout focus on-5718");

//            var c_on = pro ? Color.white : new Color(51 / 255f, 102 / 255f, 204 / 255f, 1);

//            button = new GUIStyle(EditorStyles.miniButton);
//            button.font = Font.CreateDynamicFontFromOSFont(new[] { "Terminus (TTF) for Windows", "Calibri" }, 17);

//            text = new GUIStyle(EditorStyles.label);
//            text.richText = true;
//            text.contentOffset = new Vector2(0, 5);
//            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Terminus (TTF) for Windows", "Calibri" }, 14);

//            foldout = new GUIStyle(EditorStyles.foldout);

//            foldout.overflow = new RectOffset(-10, 0, 3, 0);
//            foldout.padding = new RectOffset(25, 0, -3, 0);

//            foldout.active.textColor = c_on;
//            foldout.active.background = uiTex_in;
//            foldout.onActive.textColor = c_on;
//            foldout.onActive.background = uiTex_in_on;

//            foldout.focused.textColor = c_on;
//            foldout.focused.background = uiTex_in;
//            foldout.onFocused.textColor = c_on;
//            foldout.onFocused.background = uiTex_in_on;

//            foldout.hover.textColor = c_on;
//            foldout.hover.background = uiTex_in;

//            foldout.onHover.textColor = c_on;
//            foldout.onHover.background = uiTex_in_on;

//            box = new GUIStyle(GUI.skin.box);
//            box.padding = new RectOffset(10, 0, 10, 0);

//            boxChild = new GUIStyle(GUI.skin.box);
//            boxChild.active.textColor = c_on;
//            boxChild.active.background = uiTex_in;
//            boxChild.onActive.textColor = c_on;
//            boxChild.onActive.background = uiTex_in_on;

//            boxChild.focused.textColor = c_on;
//            boxChild.focused.background = uiTex_in;
//            boxChild.onFocused.textColor = c_on;
//            boxChild.onFocused.background = uiTex_in_on;

//            EditorStyles.foldout.active.textColor = c_on;
//            EditorStyles.foldout.active.background = uiTex_in;
//            EditorStyles.foldout.onActive.textColor = c_on;
//            EditorStyles.foldout.onActive.background = uiTex_in_on;

//            EditorStyles.foldout.focused.textColor = c_on;
//            EditorStyles.foldout.focused.background = uiTex_in;
//            EditorStyles.foldout.onFocused.textColor = c_on;
//            EditorStyles.foldout.onFocused.background = uiTex_in_on;

//            EditorStyles.foldout.hover.textColor = c_on;
//            EditorStyles.foldout.hover.background = uiTex_in;

//            EditorStyles.foldout.onHover.textColor = c_on;
//            EditorStyles.foldout.onHover.background = uiTex_in_on;
//        }

//        public static string FirstLetterToUpperCase(this string s)
//        {
//            if (string.IsNullOrEmpty(s))
//                return string.Empty;

//            var a = s.ToCharArray();
//            a[0] = char.ToUpper(a[0]);
//            return new string(a);
//        }

//        public static IList<Type> GetTypeTree(this Type t)
//        {
//            var types = new List<Type>();
//            while (t.BaseType != null)
//            {
//                types.Add(t);
//                t = t.BaseType;
//            }

//            return types;
//        }
//    }

//    static class EditorTypes
//    {
//        public static Dictionary<int, List<FieldInfo>> fields = new Dictionary<int, List<FieldInfo>>(FastComparable.Default);

//        public static int Get(Object target, out List<FieldInfo> objectFields)
//        {
//            var t = target.GetType();
//            var hash = t.GetHashCode();

//            if (!fields.TryGetValue(hash, out objectFields))
//            {
//                var typeTree = t.GetTypeTree();
//                objectFields = target.GetType()
//                        .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.NonPublic)
//                        .OrderByDescending(x => typeTree.IndexOf(x.DeclaringType))
//                        .ToList();
//                fields.Add(hash, objectFields);
//            }

//            return objectFields.Count;
//        }
//    }


//    class FastComparable : IEqualityComparer<int>
//    {
//        public static FastComparable Default = new FastComparable();

//        public bool Equals(int x, int y)
//        {
//            return x == y;
//        }

//        public int GetHashCode(int obj)
//        {
//            return obj.GetHashCode();
//        }
//    }


//    [InitializeOnLoad]
//    public static class EditorFramework
//    {
//        internal static bool needToRepaint;

//        internal static Event currentEvent;
//        internal static float t;

//        static EditorFramework()
//        {
//            EditorApplication.update += Updating;
//        }


//        static void Updating()
//        {
//            CheckMouse();

//            if (needToRepaint)
//            {
//                t += Time.deltaTime;

//                if (t >= 0.3f)
//                {
//                    t -= 0.3f;
//                    needToRepaint = false;
//                }
//            }
//        }

//        static void CheckMouse()
//        {
//            var ev = currentEvent;
//            if (ev == null) return;

//            if (ev.type == EventType.MouseMove)
//                needToRepaint = true;
//        }
//    }
//}
//#endif