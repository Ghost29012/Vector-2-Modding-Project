using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Nekki.Vector.Core
{
	/// Load editor-generated trap libraries, not unrelated XML overrides.
	public static class CustomTrapLibraryCatalog
	{
		public static IEnumerable<string> LibraryFiles()
		{
			string folder = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_gamedata", "run_data", "libraries");
			if (!Directory.Exists(folder)) yield break;
			string[] files;
			try { files = Directory.GetFiles(folder, "v2trap_*.xml", SearchOption.TopDirectoryOnly); }
			catch (Exception e) { Debug.LogWarning("[CustomTraps] Could not scan libraries: " + e.Message); yield break; }
			Array.Sort(files, StringComparer.OrdinalIgnoreCase);
			foreach (string path in files)
			{
				string filename = Path.GetFileName(path);
				if (!IsSafeLibrary(path)) { Debug.LogWarning("[CustomTraps] Ignoring invalid library " + filename); continue; }
				yield return filename;
			}
		}

		private static bool IsSafeLibrary(string path)
		{
			try
			{
				XmlDocument document = new XmlDocument();
				document.Load(path);
				XmlNode objects = document.SelectSingleNode("/Root/Objects");
				if (objects == null) return false;
				bool foundTrap = false;
				foreach (XmlNode child in objects.ChildNodes)
				{
					if (child.Name != "Object" || child.Attributes == null || child.Attributes["Name"] == null) continue;
					string name = child.Attributes["Name"].Value;
					if (!name.StartsWith("V2Trap_", StringComparison.OrdinalIgnoreCase)) return false;
					XmlNodeList triggers = child.SelectNodes(".//Trigger");
					if (triggers == null || triggers.Count == 0) return false;
					foreach (XmlNode trigger in triggers)
					{
						if (trigger.Attributes == null || trigger.Attributes["Width"] == null || trigger.Attributes["Height"] == null) return false;
						foreach (XmlNode kill in trigger.SelectNodes(".//Kill"))
							if (kill.Attributes == null || kill.Attributes["Model"] == null) return false;
						foreach (XmlNode impulse in trigger.SelectNodes(".//Impulse"))
							if (impulse.Attributes == null || impulse.Attributes["Model"] == null || impulse.Attributes["Impulse"] == null) return false;
					}
					foundTrap = true;
				}
				return foundTrap;
			}
			catch (Exception e) { Debug.LogWarning("[CustomTraps] Invalid XML: " + e.Message); }
			return false;
		}
	}
}
