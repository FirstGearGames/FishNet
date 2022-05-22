#if UNITY_EDITOR

using FishNet.Object;
using FishNet.Runtime.Editor;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Utility.Editing
{
	internal static class MiscMenuItems
	{
		/// <summary>
		/// Rebuilds sceneIds for open scenes.
		/// </summary>
		[MenuItem("Fish-Networking/Rebuild Scene Ids", false, 20)]
		private static void RebuildSceneIds()
		{
			int generatedCount = 0;

			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene s = SceneManager.GetSceneAt(i);

				SceneFN.GetSceneNetworkObjects(s, true, out ListCache<NetworkObject> nobs);

				for (int z = 0; z < nobs.Written; z++)
				{
					NetworkObject nob = nobs.Collection[z];
					
					nob.TryCreateSceneID();

					EditorUtility.SetDirty(nob);
				}

				generatedCount += nobs.Written;

				ListCaches.StoreCache(nobs);
			}

			Debug.Log($"Generated sceneIds for {generatedCount} objects over {SceneManager.sceneCount} scenes. Please save your open scenes.");
		}

		/// <summary>
		/// Regenerates the NetworkObject prefab collection.
		/// </summary>
		[MenuItem("Fish-Networking/Regenerate Prefab Objects", priority = 21)]
		private static void RegeneratePrefabObjects()
		{
			Runtime.Editor.PrefabObjects.Generation.Generator.Generate();
		}
	}
}

#endif