using UnityEditor;

namespace TriInspector.Editors
{
    public class TriSettingsProvider : SettingsProvider
    {
        private class Styles
        {
        }

        public TriSettingsProvider()
            : base("Project/Tri Inspector", SettingsScope.Project)
        {
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUI.BeginDisabledGroup(EditorApplication.isCompiling);

            base.OnGUI(searchContext);

            EditorGUI.EndDisabledGroup();
        }

        [SettingsProvider]
        public static SettingsProvider CreateTriInspectorSettingsProvider()
        {
            var provider = new TriSettingsProvider
            {
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>(),
            };

            return provider;
        }
    }
}