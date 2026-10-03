using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core.Animation;
using Nekki.Yaml;

namespace Nekki.Vector.Core.GameManagement
{
	// Add custom trick cards to the existing shop and archive card lists.
	public static class CustomTrickCards
	{
		private static readonly List<string> _CardNames = new List<string>();
		private static readonly List<string> _ManifestCardNames = new List<string>();
		private static readonly Dictionary<string, XmlElement> _Manifests = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);
		public static IList<string> CardNames { get { return _CardNames.AsReadOnly(); } }
		public static IList<string> ManifestCardNames { get { return _ManifestCardNames.AsReadOnly(); } }

		public static void MergeInto(Mapping cards)
		{
			if (cards == null) return;
			_CardNames.Clear();
			_ManifestCardNames.Clear();
			_Manifests.Clear();
			foreach (string root in CustomTrickLoader.SearchRoots(CustomProjectCatalog.StorageRoot))
			{
				if (!Directory.Exists(root)) continue;
				foreach (string path in Directory.GetFiles(root, "trick.xml", SearchOption.AllDirectories))
				{
					try { AddManifest(cards, path); }
					catch (Exception e) { UnityEngine.Debug.LogWarning("[CustomTricks] Card skipped " + path + ": " + e.Message); }
				}
			}
			string upgrades = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_upgrades");
			if (Directory.Exists(upgrades))
			{
				foreach (string path in Directory.GetFiles(upgrades, "*.xml", SearchOption.AllDirectories))
				{
					try { AddManifest(cards, path); }
					catch (Exception e) { UnityEngine.Debug.LogWarning("[CustomCards] Card skipped " + path + ": " + e.Message); }
				}
			}
		}

		private static void AddManifest(Mapping cards, string path)
		{
			XmlDocument document = new XmlDocument();
			document.XmlResolver = null;
			document.Load(path);
			XmlElement trick = document.DocumentElement;
			if (trick == null || (trick.Name != "CustomTrick" && trick.Name != "CustomCard")) return;
			bool isTrick = trick.Name == "CustomTrick";
			string itemName = Attr(trick, "Name", string.Empty);
			string cardName = Attr(trick, "CardName", isTrick ? itemName + "_1" : itemName);
			if (string.IsNullOrEmpty(cardName)) return;
			if (cards.GetNodeFast(cardName) != null) return;
			// Keep progression data available even when a creator hides a card from
			// new shop offers. Existing saves may already own and upgrade that card.
			_Manifests[cardName] = (XmlElement)trick.CloneNode(true);
			_ManifestCardNames.Add(cardName);

			string enabled = Attr(trick, "ShopEnabled", "1");
			bool shopEnabled = enabled != "0" && !enabled.Equals("false", StringComparison.OrdinalIgnoreCase);
			string visualName = Attr(trick, "VisualName", SplitName(itemName));
			string description = Attr(trick, "Description", "Custom trick: " + visualName);
			string image = Attr(trick, "Image", "stunts.track_trick_foldflip");
			string rarity = ClampInt(Attr(trick, "Rarity", "1"), 1, 3).ToString();
			string price = Math.Max(0, ParseInt(Attr(trick, "Price", "1100"), 1100)).ToString();
			string category = Attr(trick, "Category", rarity == "3" ? "Red" : rarity == "2" ? "Yellow" : "Blue");

			Mapping balance = Map("Balance",
				Text("CardPrice", price), Text("Group", Attr(trick, "Group", "CustomTricks")),
				Text("SetupMax", Attr(trick, "SetupMax", "99")), Text("SetupMin", Attr(trick, "SetupMin", "0")),
				Text("Weight", Attr(trick, "Weight", "1250")), Text("WeightForUse", Attr(trick, "WeightForUse", "1")));
			string cardType = Attr(trick, "CardType", isTrick ? "Stunts" : "Passive");
			Mapping stats = Map("Stats",
				Text("CardType", cardType), Text("Category", Attr(trick, "Category", category)), Text("EffectID", Attr(trick, "EffectID", cardType)),
				Text("Image", image), Text("Rarity", rarity), Text("RunImage", Attr(trick, "RunImage", image)), Text("Slot", Attr(trick, "Slot", cardType)),
				Text("Text", description), Text("VisualName", visualName));
			List<Nekki.Yaml.Node> variableNodes = new List<Nekki.Yaml.Node>();
			variableNodes.Add(Text("Points", Attr(trick, "Points", "?getCardParameter[" + cardName + ",Points]")));
			foreach (string key in LevelPropertyNames(trick))
			{
				variableNodes.Add(Text(key, "?getCardParameter[" + cardName + "," + key + "]"));
			}
			Mapping vars = Map("Vars", variableNodes.ToArray());
			if (isTrick) cards.Add(Map(cardName, balance, stats, Text("StuntName", itemName), vars));
			else cards.Add(Map(cardName, balance, stats, vars));
			if (shopEnabled) _CardNames.Add(cardName);
		}

		// The editor stores each custom card's progression beside its animation.
		// Stock cards still use balance.yaml/cards_levels.yaml exactly as before.
		public static int GetMaxLevel(string cardName, int fallback)
		{
			XmlElement manifest;
			if (!_Manifests.TryGetValue(cardName ?? string.Empty, out manifest)) return fallback;
			return ClampInt(Attr(manifest, "MaxLevel", fallback.ToString()), 1, 20);
		}

		public static int GetCardsRequired(string cardName, int level, int fallback)
		{
			XmlElement manifest;
			if (!_Manifests.TryGetValue(cardName ?? string.Empty, out manifest)) return fallback;
			XmlElement item = FindLevel(manifest, level);
			return item == null ? fallback : Math.Max(1, ParseInt(Attr(item, "Cards", fallback.ToString()), fallback));
		}

		public static string GetLevelParameter(string cardName, string key, int level)
		{
			XmlElement manifest;
			if (!_Manifests.TryGetValue(cardName ?? string.Empty, out manifest)) return null;
			XmlElement item = FindLevel(manifest, level);
			if (item == null || string.IsNullOrEmpty(key)) return null;
			string value = item.GetAttribute(key);
			if (!string.IsNullOrEmpty(value)) return value;
			foreach (XmlNode child in item.ChildNodes)
			{
				XmlElement property = child as XmlElement;
				if (property != null && property.Name == "Property" && Attr(property, "Name", string.Empty).Equals(key, StringComparison.OrdinalIgnoreCase))
					return Attr(property, "Value", null);
			}
			return null;
		}

		private static IEnumerable<string> LevelPropertyNames(XmlElement manifest)
		{
			HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (XmlNode child in manifest.ChildNodes)
			{
				XmlElement level = child as XmlElement;
				if (level == null || level.Name != "Level") continue;
				foreach (XmlAttribute attribute in level.Attributes)
				{
					string key = attribute.Name;
					if (key != "Number" && key != "Cards" && key != "Points" && names.Add(key)) yield return key;
				}
				foreach (XmlNode propertyNode in level.ChildNodes)
				{
					XmlElement property = propertyNode as XmlElement;
					string key = property == null || property.Name != "Property" ? string.Empty : Attr(property, "Name", string.Empty);
					if (!string.IsNullOrEmpty(key) && names.Add(key)) yield return key;
				}
			}
		}

		private static XmlElement FindLevel(XmlElement manifest, int level)
		{
			foreach (XmlNode child in manifest.ChildNodes)
			{
				XmlElement item = child as XmlElement;
				if (item != null && item.Name == "Level" && Attr(item, "Number", string.Empty) == level.ToString()) return item;
			}
			return null;
		}

		public static void AddToCurrentShop()
		{
			if (_CardNames.Count == 0) return;
			SupplyItem basket = EndFloorManager.GetOrCreateBasketItemFromBoosterpacks();
			HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (CardsGroupAttribute card in basket.Cards) existing.Add(card.CardName);
			foreach (string name in _CardNames)
			{
				CardsGroupAttribute card = CardsGroupAttribute.Create(name);
				if (!existing.Add(name)) continue;
				card.MountToItem(basket.CurrItem);
			}
		}

		private static Mapping Map(string key, params Nekki.Yaml.Node[] nodes) { return new Mapping(key, nodes); }
		private static Scalar Text(string key, string value) { return new Scalar(key, value ?? string.Empty); }
		private static string Attr(XmlElement node, string name, string fallback) { string value = node.GetAttribute(name); return string.IsNullOrEmpty(value) ? fallback : value; }
		private static int ParseInt(string value, int fallback) { int parsed; return int.TryParse(value, out parsed) ? parsed : fallback; }
		private static int ClampInt(string value, int min, int max) { return Math.Min(max, Math.Max(min, ParseInt(value, min))); }
		private static string SplitName(string value)
		{
			return System.Text.RegularExpressions.Regex.Replace(value.Replace('_', ' '), "(?<=[a-z0-9])(?=[A-Z])", " ");
		}
	}
}
