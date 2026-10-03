using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core.Counter;
using Nekki.Vector.Core.User;

namespace Nekki.Vector.Core.GameManagement
{
	/// Turns Project Manager mission definitions into the same UserItem shape the
	/// stock mission screen and counter controller already understand.
	public static class CustomMissions
	{
		private sealed class Candidate
		{
			public MissionItem Mission;
			public int Difficulty;
			public int Weight;
		}

		/// Custom missions participate in Vector's existing three difficulty slots.
		/// They never create extra cards: an eligible weighted custom candidate may
		/// replace the stock card for its difficulty.
		public static void MergeIntoSlots(List<MissionItem> missions)
		{
			if (missions == null) return;
			List<Candidate> candidates = new List<Candidate>();
			string folder = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_missions");
			if (!Directory.Exists(folder)) return;
			foreach (string path in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories))
			{
				try { LoadFile(path, candidates); }
				catch (Exception exception) { UnityEngine.Debug.LogWarning("[CustomMissions] Skipping " + path + ": " + exception.Message); }
			}
			for (int difficulty = 1; difficulty <= 3; difficulty++)
			{
				Candidate chosen = Choose(candidates, difficulty);
				if (chosen == null) continue;
				int slot = missions.FindIndex(delegate(MissionItem item) { return item != null && item.Difficulty == difficulty; });
				if (slot >= 0) missions[slot] = chosen.Mission;
				else if (missions.Count < 3) missions.Add(chosen.Mission);
				else continue;
				DataLocal.Current.AddToStash(chosen.Mission.CurrItem);
			}
		}

		private static void LoadFile(string path, List<Candidate> candidates)
		{
			XmlDocument source = new XmlDocument();
			source.XmlResolver = null;
			source.Load(path);
			XmlElement definition = source.DocumentElement;
			if (definition == null || definition.Name != "CustomMission") return;
			string id = Attr(definition, "Id", Path.GetFileNameWithoutExtension(path));
			if (string.IsNullOrEmpty(id)) return;
			int availableFloor = Number(definition, "Order", 0);
			int currentFloor = (int)CounterController.Current.CounterFloor;
			int maximumFloor = Number(definition, "MaximumFloor", 0);
			if (currentFloor < availableFloor || (maximumFloor > 0 && currentFloor > maximumFloor)) return;

			string counterName = CounterFor(Attr(definition, "Type", "Distance"), id);
			string protocol = StarterPacksManager.SelectedStarterPack == null ? "Custom" : StarterPacksManager.SelectedStarterPack.Name;
			string requiredProtocol = Attr(definition, "Protocol", "Any");
			if (!string.Equals(requiredProtocol, "Any", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(requiredProtocol, protocol, StringComparison.OrdinalIgnoreCase)) return;
			string itemName = "Mission_Custom" + Safe(id) + "_" + protocol;

			XmlDocument itemDocument = new XmlDocument();
			XmlElement itemNode = itemDocument.CreateElement("Item");
			itemDocument.AppendChild(itemNode);
			itemNode.SetAttribute("Name", itemName);
			itemNode.SetAttribute("VisualName", Attr(definition, "Name", id));
			XmlElement group = itemDocument.CreateElement("Group");
			group.SetAttribute("Name", "MissionData");
			group.SetAttribute("MissionName", Attr(definition, "Name", id));
			group.SetAttribute("MissionDescription", Attr(definition, "Description", string.Empty));
			int difficulty = Math.Max(1, Math.Min(3, Number(definition, "Difficulty", 1)));
			group.SetAttribute("Difficulty", difficulty.ToString());
			group.SetAttribute("Obj", Math.Max(1, Number(definition, "Target", 1)).ToString());
			// New editor definitions store the native credit payout directly. Keep
			// resolving legacy Reward IDs so existing projects remain compatible.
			int legacyReward = RewardCredits(Attr(definition, "Reference", string.Empty));
			group.SetAttribute("RewardAmount", Math.Max(0, Number(definition, "Amount", legacyReward)).ToString());
			group.SetAttribute("RewardId", Attr(definition, "Reference", string.Empty));
			group.SetAttribute("CounterName", counterName);
			group.SetAttribute("NoIterable", "1");
			itemNode.AppendChild(group);

			UserItem userItem = UserItem.CreateByXmlNode(itemNode);
			MissionItem mission = MissionItem.Create(userItem);
			if (mission == null) return;
			CounterController.Current.CreateCounterOrSetValue(counterName, Math.Max(1, Number(definition, "Target", 1)), "MissionObjectives");
			candidates.Add(new Candidate { Mission = mission, Difficulty = difficulty, Weight = Math.Max(1, Number(definition, "Weight", 100)) });
		}

		private static Candidate Choose(List<Candidate> candidates, int difficulty)
		{
			// Keep a stock baseline in the draw. Custom missions compete for the
			// existing slot instead of replacing it on every refresh.
			int total = 100;
			foreach (Candidate candidate in candidates) if (candidate.Difficulty == difficulty) total += candidate.Weight;
			if (total <= 0) return null;
			int roll = UnityEngine.Random.Range(0, total);
			if (roll < 100) return null;
			roll -= 100;
			foreach (Candidate candidate in candidates)
			{
				if (candidate.Difficulty != difficulty) continue;
				if (roll < candidate.Weight) return candidate;
				roll -= candidate.Weight;
			}
			return null;
		}

		private static int RewardCredits(string id)
		{
			Nekki.Vector.Core.Quest.CustomQuestRewards.Definition reward;
			return Nekki.Vector.Core.Quest.CustomQuestRewards.TryGet(id, out reward) && reward.Type == "Credits" ? reward.Amount : 0;
		}

		private static string CounterFor(string type, string fallback)
		{
			switch (type)
			{
			case "Coins": return "Money";
			case "Tricks": return "StuntsCount";
			case "Distance": return "Points";
			default: return string.IsNullOrEmpty(type) ? Safe(fallback) : type;
			}
		}

		private static int Number(XmlElement node, string name, int fallback) { int value; return int.TryParse(Attr(node, name, fallback.ToString()), out value) ? value : fallback; }
		private static string Attr(XmlElement node, string name, string fallback) { string value = node.GetAttribute(name); return string.IsNullOrEmpty(value) ? fallback : value.Trim(); }
		private static string Safe(string value) { foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_'); return value.Replace(' ', '_'); }
	}
}
