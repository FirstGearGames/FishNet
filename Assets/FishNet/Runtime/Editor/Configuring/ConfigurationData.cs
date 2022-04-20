
using System;
using System.IO;
using System.Xml.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishNet.Configuring
{

    public class ConfigurationData
    {
        //Non serialized doesn't really do anything, its just for me.
        [System.NonSerialized]
        public bool Loaded;

        public bool IsBuilding;
        public bool IsDevelopment;
        public bool IsHeadless;

        public bool StripReleaseBuilds = false;
    }

    public static class ConfigurationDataExtension
    {

        /// <summary>
        /// Returns if a differs from b.
        /// </summary>
        public static bool HasChanged(this ConfigurationData a, ConfigurationData b)
        {
            return (a.StripReleaseBuilds != b.StripReleaseBuilds);
        }
        /// <summary>
        /// Copies all values from source to target.
        /// </summary>
        public static void CopyTo(this ConfigurationData source, ConfigurationData target)
        {
            target.StripReleaseBuilds = source.StripReleaseBuilds;
        }

        /// <summary>
        /// Writes a configuration data.
        /// </summary>
        public static void Write(this ConfigurationData cd, string path, bool refreshAssetDatabase)
        {
            /* Why is this a thing you ask? Because Unity makes it VERY difficult to read values from
             * memory during builds since on some Unity versions the building application is on a different
             * processor. In result instead of using memory to read configurationdata the values
             * must be written to disk then load the disk values as needed.
             * 
             * Fortunatelly the file is extremely small and this does not occur often at all. The disk read
             * will occur once per script save, and once per assembly when building. */
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ConfigurationData));
                TextWriter writer = new StreamWriter(path);
                serializer.Serialize(writer, cd);
                writer.Close();
#if UNITY_EDITOR
                if (refreshAssetDatabase)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
#endif
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred while writing ConfigurationData. Message: {ex.Message}");
            }

        }

    }


}