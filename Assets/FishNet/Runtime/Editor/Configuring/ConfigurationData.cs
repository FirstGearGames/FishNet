
namespace FishNet.Configuring
{

    public class ConfigurationData
    {
        public bool StripReleaseBuilds = false;
    }

    internal static class ConfigurationDataExtension
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
    }


}