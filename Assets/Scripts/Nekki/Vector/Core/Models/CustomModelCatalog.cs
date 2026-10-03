using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Nekki.Vector.Core.Models
{
	public static class CustomModelCatalog
	{
		private static Dictionary<string, string> _Files;

		public static string Resolve(string skin)
		{
			if (string.IsNullOrEmpty(skin) || !skin.StartsWith("custom:", StringComparison.OrdinalIgnoreCase)) return null;
			EnsureLoaded();
			string id = skin.Substring(7);
			if (id.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) id = id.Substring(0, id.Length - 4);
			string path;
			if (_Files.TryGetValue(id, out path)) return path;
			_Files = null;
			EnsureLoaded();
			return _Files.TryGetValue(id, out path) ? path : null;
		}

		public static void Reload() { _Files = null; EnsureLoaded(); }

		private static void EnsureLoaded()
		{
			if (_Files != null) return;
			_Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			string storage = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
			string root = Path.Combine(storage, "custom_models");
			Directory.CreateDirectory(root);
			foreach (string manifestPath in Directory.GetFiles(root, "manifest.xml", SearchOption.AllDirectories))
			{
				try
				{
					XmlDocument document = new XmlDocument(); document.Load(manifestPath);
					XmlElement manifest = document.DocumentElement;
					if (manifest == null || manifest.Name != "CustomModel") throw new Exception("root must be <CustomModel>");
					string id = manifest.GetAttribute("ID");
					string fileName = manifest.GetAttribute("FileName");
					if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(fileName)) throw new Exception("ID and FileName are required");
					string package = Path.GetFullPath(Path.GetDirectoryName(manifestPath));
					string modelPath = Path.GetFullPath(Path.Combine(package, fileName));
					if (!modelPath.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(modelPath)) throw new Exception("invalid model file");
					_Files[id] = modelPath;
					Debug.Log("[CustomModels] Loaded " + id + " from " + modelPath);
				}
				catch (Exception e) { Debug.LogWarning("[CustomModels] Skipped " + manifestPath + ": " + e.Message); }
			}
		}
	}
}
