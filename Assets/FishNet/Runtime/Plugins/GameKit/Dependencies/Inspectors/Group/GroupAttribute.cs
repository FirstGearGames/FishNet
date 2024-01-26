//  Project : UNITY FOLDOUT
// Contacts : Pix - ask@pixeye.games
// https://github.com/PixeyeHQ/InspectorFoldoutGroup
// MIT license https://github.com/PixeyeHQ/InspectorFoldoutGroup/blob/master/LICENSE

using System;
using UnityEngine;

namespace GameKit.Dependencies.Inspectors
{
	public class GroupAttribute : PropertyAttribute
	{
		public string name;
		public bool foldEverything;

		/// <summary>Adds the property to the specified foldout group.</summary>
		/// <param name="name">Name of the foldout group.</param>
		/// <param name="foldEverything">Toggle to put all properties to the specified group</param>
		public GroupAttribute(string name, bool foldEverything = false)
		{
			this.foldEverything = foldEverything;
			this.name           = name;
		}
	}
}
