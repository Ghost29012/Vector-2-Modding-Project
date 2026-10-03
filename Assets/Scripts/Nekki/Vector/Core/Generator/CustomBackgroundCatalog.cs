using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Nekki.Vector.Core
{
	internal static class CustomZoneBackgrounds
	{
		private static readonly Dictionary<string, string> Picked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public static string Choose(string zoneId, string role)
		{
			if (string.IsNullOrEmpty(zoneId)) return string.Empty;
			string key = zoneId + "|" + role;
			string selected;
			if (Picked.TryGetValue(key, out selected) && ResourceManager.CustomTextureExists(selected)) return selected;
			try
			{
				string root = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
				string path = Path.Combine(root, "custom_backgrounds", "zone_backgrounds.xml");
				if (!File.Exists(path)) return string.Empty;
				XmlDocument xml = new XmlDocument();
				xml.XmlResolver = null;
				xml.Load(path);
				List<string> candidates = new List<string>();
				foreach (XmlNode item in xml.SelectNodes("/ZoneBackgrounds/Background"))
				{
					if (!string.Equals(item.Attributes?["Zone"]?.Value, zoneId, StringComparison.OrdinalIgnoreCase) ||
						!string.Equals(item.Attributes?["Role"]?.Value, role, StringComparison.OrdinalIgnoreCase)) continue;
					string file = Path.GetFileName(item.Attributes?["File"]?.Value ?? string.Empty);
					if (ResourceManager.CustomTextureExists(file)) candidates.Add(file);
				}
				if (candidates.Count == 0) return string.Empty;
				selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
				Picked[key] = selected;
				return selected;
			}
			catch (Exception error)
			{
				Debug.LogWarning("[CustomBackgrounds] Could not load zone backgrounds: " + error.Message);
				return string.Empty;
			}
		}
	}

	internal static class CustomBackgroundCatalog
	{
		private const string FolderName = "custom_backgrounds";
		private const string PoolFolderName = "custom_backgrounds_pool";
		private const string CatalogName = "custom_backgrounds.xml";
		private const string Marker = "Vector2EditorBackground:";
		private static bool _SuppressStockBackgrounds;
		private static string _PreparedRoomName = string.Empty;

		public static void PrepareForEditorRoom(string customRoomsRoot, string roomName)
		{
			_SuppressStockBackgrounds = false;
			_PreparedRoomName = string.Empty;
			if (string.IsNullOrEmpty(customRoomsRoot) || string.IsNullOrEmpty(roomName) || !Directory.Exists(customRoomsRoot)) return;

			foreach (string path in Directory.GetFiles(customRoomsRoot, "*.xml", SearchOption.AllDirectories))
			{
				if (!Path.GetFileNameWithoutExtension(path).Equals(roomName, StringComparison.OrdinalIgnoreCase)) continue;
				XmlDocument room = new XmlDocument();
				room.Load(path);
				_SuppressStockBackgrounds = !string.IsNullOrEmpty(ReadRoomMarker(room));
				_PreparedRoomName = _SuppressStockBackgrounds ? roomName : string.Empty;
				Debug.Log("[CustomBackgrounds] Stock suppression prepared=" + _SuppressStockBackgrounds + " for " + roomName);
				return;
			}
		}

		public static bool ShouldSuppressStockReference(XmlNode node)
		{
			if (!_SuppressStockBackgrounds || node == null || node.Name != "ObjectReference") return false;
			// Do not delete a background reference that came from our custom set.
			if (Attr(node, "EditorBackground") == "1") return false;
			string filename = Path.GetFileName(Attr(node, "Filename"));
			string name = Attr(node, "Name");
			return filename.Equals("background.xml", StringComparison.OrdinalIgnoreCase)
				|| name.IndexOf("placeholder_background", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public static void ApplyToRoom(XmlDocument room, string roomName, bool isRunStartRoom)
		{
			try
			{
				XmlNode content = room.SelectSingleNode("/Root/Track/Content");
				if (content == null) return;

				string requested = ReadRoomMarker(room);
				if (string.IsNullOrEmpty(requested))
				{
					// Spawn this once in the start room, just like Vector does.
					List<XmlNode> placeholders = FindStockBackgroundReferences(content);
					if (placeholders.Count == 0 && !isRunStartRoom) return;
					if (_SuppressStockBackgrounds)
					{
						RemoveStockBackgroundReferences(content);
						return;
					}
					XmlNode pooled = FindBackground(LoadCatalog(), string.Empty, roomName);
					if (pooled == null) return;
					foreach (XmlNode placeholder in placeholders)
					{
						XmlNode persistent = BuildPersistentBackground(room, pooled, placeholder);
						placeholder.ParentNode.ReplaceChild(persistent, placeholder);
					}
					// Custom entrances usually have no stock placeholder, so add ours directly.
					if (placeholders.Count == 0)
						content.AppendChild(BuildPersistentBackground(room, pooled, null, FindFloorY(content)));
					Debug.Log("[CustomBackgrounds] Attached persistent pool background '" + Attr(pooled, "Name") + "' to run start; replaced placeholders=" + placeholders.Count);
					return;
				}
				if (!string.IsNullOrEmpty(requested)) _SuppressStockBackgrounds = true;
				bool disablesStock = requested.Equals("none", StringComparison.OrdinalIgnoreCase);
				if (disablesStock)
				{
					RemoveStockBackgroundReferences(content);
					Debug.Log("[CustomBackgrounds] Disabled stock background for " + roomName);
					return;
				}
				if (!string.IsNullOrEmpty(requested))
				{
					// Explicit assignments replace the baked background even if catalog lookup fails.
					RemoveStockBackgroundReferences(content);
				}

				XmlDocument catalog = LoadCatalog();
				XmlNode background = FindBackground(catalog, requested, roomName);
				if (background == null) return;
				// Zone-tagged sets also replace the stock room reference. Without
				// this, both backgrounds render on top of each other.
				RemoveStockBackgroundReferences(content);
				int removedBakedPieces = RemoveBakedBackgroundImages(content);

				foreach (XmlNode piece in background.ChildNodes)
				{
					if (piece.NodeType == XmlNodeType.Element)
					{
						XmlNode imported = room.ImportNode(piece, true);
						MarkEditorBackgroundNodes(imported);
						content.AppendChild(imported);
					}
				}
				Debug.Log("[CustomBackgrounds] Applied '" + Attr(background, "Name") + "' to " + roomName + "; removed baked pieces=" + removedBakedPieces);
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CustomBackgrounds] " + e.Message);
			}
		}

		private static List<XmlNode> FindStockBackgroundReferences(XmlNode content)
		{
			List<XmlNode> matches = new List<XmlNode>();
			CollectStockBackgroundReferences(content, matches);
			return matches;
		}

		private static XmlNode BuildPersistentBackground(XmlDocument room, XmlNode background, XmlNode placeholder, string fallbackY = "0")
		{
			XmlElement wrapper = room.CreateElement("Object");
			wrapper.SetAttribute("Name", "EditorZoneBackground_" + Attr(background, "Name"));
			wrapper.SetAttribute("X", placeholder == null ? "0" : Attr(placeholder, "X"));
			wrapper.SetAttribute("Y", placeholder == null ? fallbackY : Attr(placeholder, "Y"));
			wrapper.SetAttribute("Factor", "0");
			wrapper.SetAttribute("MoveRoot", "1");
			XmlElement pieces = room.CreateElement("Content");
			foreach (XmlNode piece in background.ChildNodes)
				if (piece.NodeType == XmlNodeType.Element)
				{
					XmlNode imported = room.ImportNode(piece, true);
					pieces.AppendChild(imported);
				}
			wrapper.AppendChild(pieces);
			return wrapper;
		}

		private static string FindFloorY(XmlNode content)
		{
			// Stock backgrounds sit on the main floor, so custom ones should too.
			XmlNode widest = null;
			double widestWidth = double.MinValue;
			foreach (XmlNode platform in content.SelectNodes(".//Platform"))
			{
				double width;
				if (double.TryParse(Attr(platform, "Width"), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out width) && width > widestWidth)
				{
					widest = platform;
					widestWidth = width;
				}
			}
			if (widest != null && !string.IsNullOrEmpty(Attr(widest, "Y"))) return Attr(widest, "Y");
			XmlNode connector = content.SelectSingleNode(".//Out") ?? content.SelectSingleNode(".//In");
			return connector == null || string.IsNullOrEmpty(Attr(connector, "Y")) ? "0" : Attr(connector, "Y");
		}

		public static void EndEditorRun()
		{
			if (_SuppressStockBackgrounds)
			{
				Debug.Log("[CustomBackgrounds] Restoring stock backgrounds after " + _PreparedRoomName);
			}
			_SuppressStockBackgrounds = false;
			_PreparedRoomName = string.Empty;
		}

		private static void MarkEditorBackgroundNodes(XmlNode node)
		{
			if (node == null) return;
			if (node.Name == "ObjectReference" && node.Attributes != null)
			{
				XmlAttribute marker = node.OwnerDocument.CreateAttribute("EditorBackground");
				marker.Value = "1";
				node.Attributes.SetNamedItem(marker);
			}
			foreach (XmlNode child in node.ChildNodes) MarkEditorBackgroundNodes(child);
		}

		private static XmlDocument LoadCatalog(string folderName = FolderName)
		{
			string root = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
			string folder = Path.Combine(root, folderName);
			string path = Path.Combine(folder, CatalogName);
			if (!File.Exists(path)) return null;
			XmlDocument document = new XmlDocument();
			document.XmlResolver = null;
			document.Load(path);
			return document;
		}

		private static string ReadRoomMarker(XmlDocument room)
		{
			string attribute = Attr(room.DocumentElement, "CustomBackground").Trim();
			if (!string.IsNullOrEmpty(attribute)) return attribute;
			foreach (XmlNode comment in room.SelectNodes("//comment()"))
			{
				string value = (comment.Value ?? string.Empty).Trim();
				if (value.StartsWith(Marker, StringComparison.OrdinalIgnoreCase))
					return value.Substring(Marker.Length).Trim();
			}
			return string.Empty;
		}

		private static void RemoveStockBackgroundReferences(XmlNode content)
		{
			List<XmlNode> removals = new List<XmlNode>();
			CollectStockBackgroundReferences(content, removals);
			foreach (XmlNode node in removals) node.ParentNode?.RemoveChild(node);
		}

		private static void CollectStockBackgroundReferences(XmlNode node, List<XmlNode> removals)
		{
			if (node == null) return;
			if (node.Name == "ObjectReference" && Path.GetFileName(Attr(node, "Filename")).Equals("background.xml", StringComparison.OrdinalIgnoreCase)) removals.Add(node);
			foreach (XmlNode child in node.ChildNodes) CollectStockBackgroundReferences(child, removals);
		}

		private static int RemoveBakedBackgroundImages(XmlNode content)
		{
			List<XmlNode> removals = new List<XmlNode>();
			CollectBakedBackgroundImages(content, removals);
			foreach (XmlNode node in removals) node.ParentNode?.RemoveChild(node);
			return removals.Count;
		}

		private static void CollectBakedBackgroundImages(XmlNode node, List<XmlNode> removals)
		{
			if (node == null) return;
			if (node.Name == "Image" || node.Name == "CustomAnimation")
			{
				string tag = Attr(node, "Tag");
				string layer = Attr(node, "Layer");
				if (tag.Equals("Background", StringComparison.OrdinalIgnoreCase)
					|| layer.StartsWith("Bg", StringComparison.OrdinalIgnoreCase))
				{
					removals.Add(node);
					return;
				}
			}
			foreach (XmlNode child in node.ChildNodes) CollectBakedBackgroundImages(child, removals);
		}

		private static XmlNode FindBackground(XmlDocument catalog, string requested, string roomName)
		{
			if (string.IsNullOrEmpty(requested))
			{
				if (!GameManagement.ZoneManager.IsCustomZoneActive) return null;
				List<XmlNode> matches = new List<XmlNode>();
				string zoneId = GameManagement.ZoneManager.CurrentZoneId;
				// Keep pool backgrounds away from the per-room ones. Windows can read this too.
				XmlDocument pool = LoadCatalog(PoolFolderName);
				string storageRoot = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
				string poolFolder = Path.Combine(storageRoot, PoolFolderName);
				if (Directory.Exists(poolFolder))
				{
					foreach (string file in Directory.GetFiles(poolFolder, "*.xml", SearchOption.TopDirectoryOnly))
					{
						if (Path.GetFileName(file).Equals(CatalogName, StringComparison.OrdinalIgnoreCase)) continue;
						try
						{
							XmlDocument individual = new XmlDocument();
							individual.XmlResolver = null;
							individual.Load(file);
							XmlNode item = individual.SelectSingleNode("/Background");
							if (item != null && Attr(item, "Zone").Equals(zoneId, StringComparison.OrdinalIgnoreCase))
							{
							// Older editor builds wrote palette selections into pool images.
							// Those are not room variants; leaving them filters every image out.
							foreach (XmlNode image in item.SelectNodes(".//Image | .//CustomAnimation"))
							{
								XmlNode staticNode = image.SelectSingleNode("Properties/Static");
								if (staticNode == null) continue;
								XmlNodeList gates = staticNode.SelectNodes("Selection");
							for (int gate = gates.Count - 1; gate >= 0; gate--) staticNode.RemoveChild(gates[gate]);
							}
							NormalizePoolBackground(item);
							matches.Add(item);
							}
						}
						catch (Exception error) { Debug.LogWarning("[CustomBackgrounds] Invalid pool file " + file + ": " + error.Message); }
					}
				}
				if (matches.Count > 0) return matches[UnityEngine.Random.Range(0, matches.Count)];
				XmlNodeList poolNodes = pool?.SelectNodes("/CustomBackgrounds/Background");
				if (poolNodes != null)
					foreach (XmlNode node in poolNodes)
						if (Attr(node, "Zone").Equals(zoneId, StringComparison.OrdinalIgnoreCase)) matches.Add(node);
				// Once a zone has a real pool, don't mix the older room catalog into
				// its random picks. Keep the old tags only as a migration fallback.
				if (matches.Count > 0) return matches[UnityEngine.Random.Range(0, matches.Count)];
				XmlNodeList legacyNodes = catalog?.SelectNodes("/CustomBackgrounds/Background");
				if (legacyNodes != null)
					foreach (XmlNode node in legacyNodes)
						if (Attr(node, "Zone").Equals(zoneId, StringComparison.OrdinalIgnoreCase)) matches.Add(node);
				return matches.Count == 0 ? null : matches[UnityEngine.Random.Range(0, matches.Count)];
			}
			XmlNodeList nodes = catalog?.SelectNodes("/CustomBackgrounds/Background");
			if (nodes != null)
				foreach (XmlNode node in nodes)
					if (Attr(node, "Name").Equals(requested, StringComparison.OrdinalIgnoreCase)) return node;
			// A room can explicitly choose a saved zone-pool background. Resolve
			// that name before falling back; otherwise the room's previewed art is
			// removed and the random pool supplies a different set at runtime.
			string poolRoot = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
			string namedPoolFolder = Path.Combine(poolRoot, PoolFolderName);
			if (Directory.Exists(namedPoolFolder))
			{
				foreach (string file in Directory.GetFiles(namedPoolFolder, "*.xml", SearchOption.TopDirectoryOnly))
				{
					if (!Path.GetFileNameWithoutExtension(file).Equals(requested, StringComparison.OrdinalIgnoreCase)) continue;
					try
					{
						XmlDocument individual = new XmlDocument();
						individual.XmlResolver = null;
						individual.Load(file);
						XmlNode selected = individual.SelectSingleNode("/Background");
						if (selected != null)
						{
							NormalizePoolBackground(selected);
							return selected;
						}
					}
					catch (Exception error) { Debug.LogWarning("[CustomBackgrounds] Invalid selected pool file " + file + ": " + error.Message); }
				}
			}
			Debug.LogWarning("[CustomBackgrounds] Missing requested background '" + requested + "'");
			return null;
		}

		// Every parallax layer needs enough artwork below the floor to cover how
		// far it can lag behind the camera.
		private static void NormalizePoolBackground(XmlNode background)
		{
			XmlNodeList pieces = background?.SelectNodes(".//Image | .//CustomAnimation");
			if (pieces == null || pieces.Count == 0) return;
			int left = int.MaxValue;
			Dictionary<string, int> bottoms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, int> bleedByLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (XmlNode piece in pieces)
			{
				int x;
				int y;
				int height;
				if (!int.TryParse(Attr(piece, "X"), out x)) x = 0;
				if (!int.TryParse(Attr(piece, "Y"), out y)) y = 0;
				if (!int.TryParse(Attr(piece, "Height"), out height)) height = 0;
				left = Math.Min(left, x);
				string layer = Attr(piece, "Layer");
				int edge = y + Math.Max(0, height);
				int current;
				if (!bottoms.TryGetValue(layer, out current) || edge > current) bottoms[layer] = edge;
				double factor;
				if (!double.TryParse(Attr(piece, "Factor"), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out factor)) factor = 1;
				factor = Math.Min(1, Math.Max(0, factor));
				int bleed = (int)Math.Ceiling(Math.Max(0, height) * (1 - factor));
				if (!bleedByLayer.TryGetValue(layer, out current) || bleed > current) bleedByLayer[layer] = bleed;
			}
			foreach (XmlNode piece in pieces)
			{
				int x;
				int y;
				if (!int.TryParse(Attr(piece, "X"), out x)) x = 0;
				if (!int.TryParse(Attr(piece, "Y"), out y)) y = 0;
				Set(piece, "X", x - left);
				string layer = Attr(piece, "Layer");
				Set(piece, "Y", y - bottoms[layer] + bleedByLayer[layer]);
			}
		}

		private static void Set(XmlNode node, string name, object value)
		{
			XmlAttribute attribute = node?.Attributes?[name];
			if (attribute != null) attribute.Value = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
		}

		private static string Attr(XmlNode node, string name)
		{
			return node?.Attributes?[name]?.Value ?? string.Empty;
		}
	}
}
