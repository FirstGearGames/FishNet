using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Data
{

    /// <summary>
    /// Contains information to look up a scene.
    /// </summary>
    public class SceneReferenceData
    {
        /// <summary>
        /// Handle of the scene. If value is 0, then handle is not used.
        /// </summary>
        public int Handle;
        /// <summary>
        /// Name of the scene.
        /// </summary>
        public string Name;

        public SceneReferenceData() { }
        public SceneReferenceData(Scene scene)
        {
            Handle = scene.handle;
            Name = scene.name;
        }

        public static bool operator ==(SceneReferenceData srdA, SceneReferenceData srdB)
        {
            //One is null while the other is not.
            if ((srdA is null) != (srdB is null))
                return false;

            /*If here both are either null or have value. */
            if (!(srdA is null))
                return srdA.Equals(srdB);
            else if (!(srdB is null))
                return srdB.Equals(srdA);

            //Fall through indicates both are null.
            return true;
        }

        public static bool operator !=(SceneReferenceData srdA, SceneReferenceData srdB)
        {
            //One is null while the other is not.
            if ((srdA is null) != (srdB is null))
                return true;

            /*If here both are either null or have value. */
            if (!(srdA is null))
                return !srdA.Equals(srdB);
            else if (!(srdB is null))
                return !srdB.Equals(srdA);

            //Fall through indicates both are null.
            return true;
        }

        public bool Equals(SceneReferenceData srd)
        {
            //Comparing instanced against null.
            if (srd is null)
                return false;

            //True if both handles are empty.
            bool bothHandlesEmpty = (
                (this.Handle == 0) &&
                (srd.Handle == 0)
                );

            //If both have handles and they match.
            if (!bothHandlesEmpty && srd.Handle == this.Handle)
                return true;
            //If neither have handles and name matches.
            else if (bothHandlesEmpty && srd.Name == this.Name)
                return true;

            //Fall through.
            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = 2053068273;
            hashCode = hashCode * -1521134295 + Handle.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }


}