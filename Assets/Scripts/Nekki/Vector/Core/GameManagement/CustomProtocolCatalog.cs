using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Nekki.Vector.Core.GameManagement
{
	// Read custom protocol XML as starter-pack items for the floor menu and runs.
	public static class CustomProtocolCatalog
	{
		public sealed class Definition
		{
			public string Id;
			public string Name;
			public string Description;
			public string Artwork;
			public string ChapterId;
			public string BaseProtocol;
			public string PlayerModel;
			public int StartFloor;
			public int Order;
			public string EffectId;
			public float EffectInterval;
			public int EffectAmount;
			public int EffectMaximum;
			public int ArmorCapacity;
			public int ImpactResistance;
			public int HeatResistance;
			public int ElectricResistance;
			public int SwarmResistance;
			public int HelmetDurability;
			public int TorsoDurability;
			public int HandsDurability;
			public int LegsDurability;
			public int BeltDurability;
			public readonly List<string> ArmorModels = new List<string>();
		}

		private static readonly Dictionary<string, Definition> _MappedByStarterPack = new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase);

		public static List<Definition> GetForCurrentChapter()
		{
			List<Definition> result = new List<Definition>();
			CustomProjectCatalog.Chapter chapter;
			if (!CustomProjectCatalog.TryGetCurrentChapter(out chapter)) return result;
			string folder = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_protocols");
			Directory.CreateDirectory(folder);
			foreach (string path in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories))
			{
				try
				{
					XmlDocument document = new XmlDocument(); document.Load(path);
					XmlNodeList nodes = document.SelectNodes("/Protocols/Protocol");
					if (nodes == null) continue;
					foreach (XmlNode node in nodes)
					{
						Definition item = Parse(node);
						if (item != null && string.Equals(item.ChapterId, chapter.Id, StringComparison.OrdinalIgnoreCase)) result.Add(item);
					}
				}
				catch (Exception e) { UnityEngine.Debug.LogWarning("[CustomProtocols] Skipped " + path + ": " + e.Message); }
			}
			result.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.StartFloor.CompareTo(b.StartFloor));
			return result;
		}

		public static void BeginMapping() { _MappedByStarterPack.Clear(); }

		public static void Map(string starterPackName, Definition definition)
		{
			if (!string.IsNullOrEmpty(starterPackName) && definition != null) _MappedByStarterPack[starterPackName] = definition;
		}

		public static bool TryGet(string starterPackName, out Definition definition)
		{
			return _MappedByStarterPack.TryGetValue(starterPackName ?? string.Empty, out definition);
		}

		public static bool TryGetSelected(out Definition definition)
		{
			StarterPackItem selected = StarterPacksManager.SelectedStarterPack;
			if (selected == null) { definition = null; return false; }
			return TryGet(selected.Name, out definition);
		}

		public static List<string> SelectedModelLayers
		{
			get
			{
				Definition definition;
				StarterPackItem selected = StarterPacksManager.SelectedStarterPack;
				if (selected == null || !TryGet(selected.Name, out definition)) return null;
				List<string> result = new List<string>();
				if (!string.IsNullOrEmpty(definition.PlayerModel)) result.Add(definition.PlayerModel);
				foreach (string layer in definition.ArmorModels) if (!string.IsNullOrEmpty(layer) && !result.Contains(layer)) result.Add(layer);
				return result;
			}
		}

		private static Definition Parse(XmlNode node)
		{
			string id = Attr(node, "Id");
			if (string.IsNullOrEmpty(id)) return null;
			Definition result = new Definition {
				Id = id,
				Name = Attr(node, "Name", id),
				Description = Attr(node, "Description"),
				Artwork = Attr(node, "Artwork", "box_unknown"),
				ChapterId = Attr(node, "Chapter"),
				BaseProtocol = Attr(node, "BaseProtocol", "BasicProtocol"),
				PlayerModel = NormalizeModel(Attr(node, "PlayerModel")),
				StartFloor = IntAttr(node, "StartFloor", 1),
				Order = IntAttr(node, "Order", 0)
			};
			XmlNode gameplay = node.SelectSingleNode("Gameplay");
			result.EffectId = Attr(gameplay, "EffectID", "None");
			result.EffectInterval = FloatAttr(gameplay, "Interval", 5f);
			result.EffectAmount = IntAttr(gameplay, "Amount", 1);
			result.EffectMaximum = IntAttr(gameplay, "Maximum", 12);
			XmlNode defense = node.SelectSingleNode("ArmourDefense");
			result.ArmorCapacity = IntAttr(defense, "Capacity", 100);
			result.ImpactResistance = IntAttr(defense, "Impact", 100);
			result.HeatResistance = IntAttr(defense, "Heat", 100);
			result.ElectricResistance = IntAttr(defense, "Electric", 100);
			result.SwarmResistance = IntAttr(defense, "Swarm", 100);
			result.HelmetDurability = IntAttr(defense, "Helmet", result.ArmorCapacity);
			result.TorsoDurability = IntAttr(defense, "Torso", result.ArmorCapacity);
			result.HandsDurability = IntAttr(defense, "Hands", result.ArmorCapacity);
			result.LegsDurability = IntAttr(defense, "Legs", result.ArmorCapacity);
			result.BeltDurability = IntAttr(defense, "Belt", result.ArmorCapacity);
			XmlNodeList armorNodes = node.SelectNodes("Armor");
			if (armorNodes != null) foreach (XmlNode armor in armorNodes)
			{
				string model = NormalizeModel(Attr(armor, "Model"));
				if (!string.IsNullOrEmpty(model)) result.ArmorModels.Add(model);
			}
			return result;
		}

		private static string NormalizeModel(string value)
		{
			if (string.IsNullOrEmpty(value)) return string.Empty;
			return value.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) ? value : "custom:" + value;
		}

		private static string Attr(XmlNode node, string name, string fallback = "")
		{
			XmlAttribute attribute = node == null ? null : node.Attributes[name];
			return attribute == null || string.IsNullOrEmpty(attribute.Value) ? fallback : attribute.Value;
		}

		private static int IntAttr(XmlNode node, string name, int fallback)
		{
			int value; return int.TryParse(Attr(node, name), out value) ? value : fallback;
		}

		private static float FloatAttr(XmlNode node, string name, float fallback)
		{
			float value; return float.TryParse(Attr(node, name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback;
		}
	}
}
