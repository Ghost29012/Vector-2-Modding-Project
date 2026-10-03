using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core.Animation;
using Nekki.Vector.Core.GameManagement;
using UnityEngine;

namespace Nekki.Vector.Core
{
	// This is the shared contract between the Project Manager and the game.
	// Stock Vector 2 still uses its Zone enum; custom content uses stable string IDs
	// so adding a chapter never requires recompiling the game.
	public static class CustomProjectCatalog
	{
		public sealed class Chapter
		{
			public string Id;
			public string Name;
			public string Description;
			public string Artwork;
			public readonly List<string> ZoneIds = new List<string>();
			public readonly List<int> StartFloors = new List<int>();
		}

		public sealed class ZoneDefinition
		{
			public string Id;
			public string ChapterId;
			public string Name;
			public string Description;
			public string RoomsPath;
			public string Artwork;
			public string MainBackground;
			public string LoaderBackground;
			public string TricksPath;
			public int Order;
			public readonly Dictionary<string, string> Resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}

		public sealed class Speaker
		{
			public string Id;
			public string Name;
			public string Portrait;
			public string Color;
		}

		public sealed class Dialogue
		{
			public string Id;
			public string SpeakerId;
			public string Title;
			public string Text;
			public string Image;
			public string Button;
			public string Trigger;
			public string Reference;
			public bool Once;
		}

		private static readonly Dictionary<string, Chapter> _Chapters = new Dictionary<string, Chapter>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, ZoneDefinition> _Zones = new Dictionary<string, ZoneDefinition>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, Speaker> _Speakers = new Dictionary<string, Speaker>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, Dialogue> _Dialogues = new Dictionary<string, Dialogue>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> _Phrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly List<XmlNode> _QuestNodes = new List<XmlNode>();
		private static bool _Loaded;

		public static ICollection<Chapter> Chapters { get { EnsureLoaded(); return _Chapters.Values; } }
		public static ICollection<ZoneDefinition> Zones { get { EnsureLoaded(); return _Zones.Values; } }
		public static ICollection<Speaker> Speakers { get { EnsureLoaded(); return _Speakers.Values; } }
		public static ICollection<Dialogue> Dialogues { get { EnsureLoaded(); return _Dialogues.Values; } }

		public static string StorageRoot
		{
			get { return !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath; }
		}

		public static void EnsureFolders()
		{
			string root = StorageRoot;
			Directory.CreateDirectory(Path.Combine(root, "custom_chapters"));
			Directory.CreateDirectory(Path.Combine(root, "custom_zones"));
			Directory.CreateDirectory(Path.Combine(root, "custom_quests"));
			Directory.CreateDirectory(Path.Combine(root, "custom_dialogue"));
			Directory.CreateDirectory(Path.Combine(root, "custom_characters"));
			Directory.CreateDirectory(Path.Combine(root, "custom_localization"));
			Directory.CreateDirectory(Path.Combine(root, "custom_audio"));
			Directory.CreateDirectory(Path.Combine(root, "custom_gamedata"));
			Directory.CreateDirectory(Path.Combine(root, "custom_economy"));
			Directory.CreateDirectory(Path.Combine(root, "custom_upgrades"));
			Directory.CreateDirectory(Path.Combine(root, "custom_missions"));
			Directory.CreateDirectory(Path.Combine(root, "custom_rewards"));
			Directory.CreateDirectory(Path.Combine(root, "custom_tutorials"));
			Directory.CreateDirectory(Path.Combine(root, "custom_protocols"));
			Directory.CreateDirectory(Path.Combine(root, "custom_traps"));
			Directory.CreateDirectory(Path.Combine(root, "custom_story"));
		}

		public static void Reload()
		{
			_Loaded = false;
			_Chapters.Clear();
			_Zones.Clear();
			_Speakers.Clear();
			_Dialogues.Clear();
			_Phrases.Clear();
			_QuestNodes.Clear();
			EnsureLoaded();
		}

		public static bool TryGetChapter(string id, out Chapter value) { EnsureLoaded(); return _Chapters.TryGetValue(id ?? string.Empty, out value); }
		public static bool TryGetZone(string id, out ZoneDefinition value) { EnsureLoaded(); return _Zones.TryGetValue(id ?? string.Empty, out value); }
		public static bool TryGetSpeaker(string id, out Speaker value) { EnsureLoaded(); return _Speakers.TryGetValue(id ?? string.Empty, out value); }
		public static bool TryGetDialogue(string id, out Dialogue value) { EnsureLoaded(); return _Dialogues.TryGetValue(id ?? string.Empty, out value); }

		public static bool TryGetCurrentChapter(out Chapter value)
		{
			value = null;
			ZoneDefinition zone;
			if (!ZoneManager.IsCustomZoneActive || !TryGetZone(ZoneManager.CurrentZoneId, out zone)) return false;
			return !string.IsNullOrEmpty(zone.ChapterId) && TryGetChapter(zone.ChapterId, out value);
		}

		// Project files store portable paths such as custom_rooms/chapter_3. Resolve
		// them here so no caller can accidentally escape Vector's mod-data folder.
		public static string ResolveProjectFolder(string relativePath, string fallbackFolder)
		{
			string root = Path.GetFullPath(StorageRoot);
			string relative = string.IsNullOrEmpty(relativePath) ? fallbackFolder : relativePath;
			relative = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
			string resolved = Path.GetFullPath(Path.Combine(root, relative));
			if (resolved != root && !resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				return Path.Combine(root, fallbackFolder);
			return resolved;
		}

		public static bool TryGetPhrase(string key, SystemLanguage language, out string value)
		{
			EnsureLoaded();
			if (_Phrases.TryGetValue(language + "|" + key, out value)) return true;
			return _Phrases.TryGetValue("Default|" + key, out value);
		}

		// QuestManager consumes clones, because an XmlNode can only belong to one
		// document and the stock quest parser keeps references to its descendants.
		public static IList<XmlNode> GetQuestNodes()
		{
			EnsureLoaded();
			List<XmlNode> result = new List<XmlNode>();
			foreach (XmlNode node in _QuestNodes) result.Add(node.CloneNode(true));
			return result;
		}

		private static void EnsureLoaded()
		{
			if (_Loaded) return;
			_Loaded = true;
			try
			{
				EnsureFolders();
				LoadChapters();
				LoadZones();
				LoadSpeakers();
				LoadDialogues();
				LoadLocalization();
				LoadQuests();
				Debug.Log("[CustomProject] Loaded chapters=" + _Chapters.Count + ", zones=" + _Zones.Count
					+ ", quests=" + _QuestNodes.Count + ", dialogue=" + _Dialogues.Count + ", speakers=" + _Speakers.Count);
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CustomProject] Catalog load failed: " + e.Message);
			}
		}

		private static IEnumerable<XmlDocument> Documents(string folder)
		{
			string path = Path.Combine(StorageRoot, folder);
			if (!Directory.Exists(path)) yield break;
			foreach (string file in Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories))
			{
				XmlDocument document = new XmlDocument();
				document.XmlResolver = null;
				try { document.Load(file); }
				catch (Exception e)
				{
					Debug.LogWarning("[CustomProject] Skipping " + file + ": " + e.Message);
					continue;
				}
				yield return document;
			}
		}

		private static void LoadChapters()
		{
			foreach (XmlDocument doc in Documents("custom_chapters"))
			foreach (XmlNode node in doc.SelectNodes("//Chapter"))
			{
				Chapter item = new Chapter();
				item.Id = Attr(node, "Id");
				if (!ValidId(item.Id) || _Chapters.ContainsKey(item.Id)) continue;
				item.Name = Attr(node, "Name");
				item.Description = Attr(node, "Description");
				item.Artwork = Attr(node, "Artwork");
				foreach (XmlNode zone in node.SelectNodes("Zone")) AddUnique(item.ZoneIds, Attr(zone, "Id"));
				foreach (XmlNode floor in node.SelectNodes("Floor"))
				{
					int number;
					if (int.TryParse(Attr(floor, "Number"), out number) && number > 0 && !item.StartFloors.Contains(number)) item.StartFloors.Add(number);
				}
				if (item.StartFloors.Count == 0) item.StartFloors.Add(1);
				_Chapters.Add(item.Id, item);
			}
		}

		private static void LoadZones()
		{
			foreach (XmlDocument doc in Documents("custom_zones"))
			foreach (XmlNode node in doc.SelectNodes("//Zone"))
			{
				ZoneDefinition item = new ZoneDefinition();
				item.Id = Attr(node, "Id");
				if (!ValidId(item.Id) || _Zones.ContainsKey(item.Id)) continue;
				item.ChapterId = Attr(node, "Chapter");
				item.Name = Attr(node, "Name");
				item.Description = Attr(node, "Description");
				item.RoomsPath = Attr(node, "RoomsPath");
				item.Artwork = Attr(node, "Artwork");
				item.MainBackground = Attr(node, "MainBackground");
				item.LoaderBackground = Attr(node, "LoaderBackground");
				item.TricksPath = Attr(node, "TricksPath");
				int.TryParse(Attr(node, "Order"), out item.Order);
				foreach (XmlNode resource in node.SelectNodes("Resource"))
				{
					string key = Attr(resource, "Id");
					if (ValidId(key)) item.Resources[key] = Attr(resource, "Path");
				}
				_Zones.Add(item.Id, item);
				Chapter chapter;
				if (_Chapters.TryGetValue(item.ChapterId, out chapter)) AddUnique(chapter.ZoneIds, item.Id);
			}
		}

		private static void LoadSpeakers()
		{
			foreach (XmlDocument doc in Documents("custom_characters"))
			foreach (XmlNode node in doc.SelectNodes("//Character|//Speaker"))
			{
				Speaker item = new Speaker();
				item.Id = Attr(node, "Id");
				if (!ValidId(item.Id) || _Speakers.ContainsKey(item.Id)) continue;
				item.Name = Attr(node, "Name");
				item.Portrait = Attr(node, "Portrait");
				item.Color = Attr(node, "Color");
				_Speakers.Add(item.Id, item);
			}
		}

		private static void LoadDialogues()
		{
			foreach (XmlDocument doc in Documents("custom_dialogue"))
			foreach (XmlNode node in doc.SelectNodes("//Dialogue"))
			{
				Dialogue item = new Dialogue();
				item.Id = Attr(node, "Id");
				if (!ValidId(item.Id) || _Dialogues.ContainsKey(item.Id)) continue;
				item.SpeakerId = Attr(node, "Speaker");
				item.Title = Attr(node, "Title");
				item.Text = Attr(node, "Text");
				item.Image = Attr(node, "Image");
				item.Button = Attr(node, "Button");
				item.Trigger = Attr(node, "Trigger");
				item.Reference = Attr(node, "Reference");
				item.Once = Attr(node, "Once") != "0";
				_Dialogues.Add(item.Id, item);
			}
		}

		private static void LoadLocalization()
		{
			foreach (XmlDocument doc in Documents("custom_localization"))
			foreach (XmlNode node in doc.SelectNodes("//Phrase"))
			{
				string key = Attr(node, "Key");
				if (!ValidId(key)) continue;
				string language = Attr(node, "Language");
				if (string.IsNullOrEmpty(language)) language = "Default";
				_Phrases[language + "|" + key] = Attr(node, "Value");
			}

			// Several stock stunt screens ignore Cards.Stats.VisualName and rebuild
			// a localization alias from StuntName instead. Give custom tricks the
			// same aliases stock tricks have so names/descriptions never fall
			// through to <PhraseError-...>.
			LoadCustomTrickLocalization();
		}

		private static void LoadCustomTrickLocalization()
		{
			HashSet<string> manifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string root in CustomTrickLoader.SearchRoots(StorageRoot))
			{
				if (!Directory.Exists(root)) continue;
				foreach (string path in Directory.GetFiles(root, "trick.xml", SearchOption.AllDirectories))
					manifests.Add(path);
			}

			foreach (string path in manifests)
			{
				try
				{
					XmlDocument doc = new XmlDocument();
					doc.XmlResolver = null;
					doc.Load(path);
					XmlElement trick = doc.DocumentElement;
					if (trick == null || trick.Name != "CustomTrick") continue;

					string name = trick.GetAttribute("Name").Trim();
					if (!ValidId(name)) continue;
					string cardName = trick.GetAttribute("CardName").Trim();
					if (string.IsNullOrEmpty(cardName)) cardName = name + "_1";
					string visualName = trick.GetAttribute("VisualName").Trim();
					if (string.IsNullOrEmpty(visualName)) visualName = HumanizeId(name);
					string description = trick.GetAttribute("Description").Trim();
					if (string.IsNullOrEmpty(description)) description = "Custom trick: " + visualName;

					AddDefaultPhrase("Stunts.StuntDetails." + name + ".StuntVisualItemName", visualName);
					AddDefaultPhrase("Stunts.StuntDetails." + name + ".Text", description);
					if (ValidId(cardName))
					{
						AddDefaultPhrase("Stunts.StuntDetails." + cardName + ".StuntVisualItemName", visualName);
						AddDefaultPhrase("Stunts.StuntDetails." + cardName + ".Text", description);
					}
				}
				catch (Exception e)
				{
					Debug.LogWarning("[CustomTricks] Could not build localization for " + path + ": " + e.Message);
				}
			}
		}

		private static void AddDefaultPhrase(string key, string value)
		{
			string mapKey = "Default|" + key;
			if (!_Phrases.ContainsKey(mapKey)) _Phrases[mapKey] = value ?? string.Empty;
		}

		private static string HumanizeId(string value)
		{
			if (string.IsNullOrEmpty(value)) return string.Empty;
			System.Text.StringBuilder result = new System.Text.StringBuilder(value.Length + 8);
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i] == '_' ? ' ' : value[i];
				if (i > 0 && char.IsUpper(c) && char.IsLetterOrDigit(value[i - 1]) && value[i - 1] != '_') result.Append(' ');
				result.Append(c);
			}
			return result.ToString();
		}

		private static void LoadQuests()
		{
			HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (XmlDocument doc in Documents("custom_quests"))
			foreach (XmlNode node in doc.SelectNodes("/Quests/Quest|//Quest"))
			{
				string id = Attr(node, "Name");
				if (!ValidId(id) || !ids.Add(id)) continue;
				_QuestNodes.Add(node.CloneNode(true));
			}
		}

		private static string Attr(XmlNode node, string name) { return node == null || node.Attributes == null || node.Attributes[name] == null ? string.Empty : node.Attributes[name].Value.Trim(); }
		private static bool ValidId(string value) { return !string.IsNullOrEmpty(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && value.IndexOf('/') < 0 && value.IndexOf('\\') < 0; }
		private static void AddUnique(List<string> values, string value) { if (ValidId(value) && !values.Contains(value)) values.Add(value); }
	}
}
