using System;
using System.IO;
using System.Xml;

namespace Nekki.Vector.Core.Quest
{
	/// Read Project Manager reward definitions for the existing quest reward flow.
	public static class CustomQuestRewards
	{
		public sealed class Definition
		{
			public string Id;
			public string Type;
			public int Amount;
			public string Reference;
		}

		public static bool TryGet(string id, out Definition result)
		{
			result = null;
			if (string.IsNullOrEmpty(id)) return false;
			string folder = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_rewards");
			if (!Directory.Exists(folder)) return false;
			foreach (string path in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories))
			{
				try
				{
					XmlDocument document = new XmlDocument();
					document.XmlResolver = null;
					document.Load(path);
					XmlElement root = document.DocumentElement;
					if (root == null || root.Name != "CustomReward" || !Attr(root, "Id").Equals(id, StringComparison.OrdinalIgnoreCase)) continue;
					int amount;
					if (!int.TryParse(Attr(root, "Amount"), out amount)) amount = 1;
					result = new Definition { Id = id, Type = Attr(root, "Type"), Amount = Math.Max(1, amount), Reference = Attr(root, "Reference") };
					return true;
				}
				catch (Exception exception) { UnityEngine.Debug.LogWarning("[CustomRewards] Skipping " + path + ": " + exception.Message); }
			}
			return false;
		}

		private static string Attr(XmlElement element, string name)
		{
			string value = element.GetAttribute(name);
			return value == null ? string.Empty : value.Trim();
		}
	}
}
