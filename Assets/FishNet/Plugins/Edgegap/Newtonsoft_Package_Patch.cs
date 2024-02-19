#if !NEWTONSOFT_JSON
using System;

#if UNITY_EDITOR
using UnityEditor;
  
namespace Newtonsoft.Json
{ 
    public static class FallbackDisplayer
    {


        [InitializeOnLoadMethod]
        private static void Initialize()
        {
                UnityEngine.Debug.LogWarning($"Edgegap requires Json.NET to be imported to function. To import Json.NET navigate to Window -> Package Manager -> Click the + symbol and choose 'Add package by name' -> com.unity.nuget.newtonsoft-json -> Leave version blank and click Add.");
        }
    }
}
#endif

namespace Newtonsoft.Json
{


    public class JsonPropertyAttribute : Attribute
    {
        public string PropertyName;
        public JsonPropertyAttribute() { }
        public JsonPropertyAttribute(string a) { }
    }


    public class JsonIgnoreAttribute : Attribute
    {
    }


    public static class JsonConvert
    {
        public static string SerializeObject(object obj, Formatting format) => default;
        public static string SerializeObject(object obj) => default;
        public static object DeserializeObject(string str) => default;
        public static T DeserializeObject<T>(string str) => default;
    }

    public enum Formatting
    {
        None = 0,
        Indented = 1,
    }
}

namespace Newtonsoft.Json.Linq
{
    public class JObject
    {
        public string this[string position]
        {
            get => string.Empty;
            set { }
        }
        public JObject() { }
        public JObject(object content) { }
        public JObject(params object[] content) { }
        public JObject(JObject content) { }
        public static JObject Parse(string json) => default;

    }

}
#endif