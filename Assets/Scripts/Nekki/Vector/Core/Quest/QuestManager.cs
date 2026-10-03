using System.Collections.Generic;
using System.Xml;
using CodeStage.AntiCheat.ObscuredTypes;
using Nekki.Vector.Core.Counter;
using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.Core.Trigger;
using Nekki.Vector.Core.User;
using Nekki.Vector.GUI;
using Nekki.Vector.GUI.MainScene;
using Nekki.Yaml;

namespace Nekki.Vector.Core.Quest
{
	public class QuestManager
	{
		private const string _QuestsFileName = "quests.xml";

		private const string _QuestRewardsFileName = "quest_rewards.yaml";

		private List<Quest> _Quests = new List<Quest>();

		private Dictionary<string, Preset> _Rewards = new Dictionary<string, Preset>();

		private static QuestManager _Current;

		public List<Quest> Quests
		{
			get
			{
				return _Quests;
			}
		}

		public bool HasNewQuest
		{
			get
			{
				foreach (Quest quest in _Quests)
				{
					if ((int)CounterController.Current.GetUserCounter(quest.Name, "ST_NewQuest") == 1)
					{
						return true;
					}
				}
				return false;
			}
		}

		public bool HasUncompletedQuest
		{
			get
			{
				for (int i = 0; i < Quests.Count; i++)
				{
					if (Quests[i].QuestState != Quest.State.Complete)
					{
						return true;
					}
				}
				return false;
			}
		}

		public static QuestManager Current
		{
			get
			{
				if (_Current == null)
				{
					Init();
				}
				return _Current;
			}
		}

		private QuestManager()
		{
			ParseQuests();
			ParseRewards();
		}

		public static void Init()
		{
			if (_Current == null)
			{
				_Current = new QuestManager();
			}
		}

		public static void Reset()
		{
			_Current = null;
		}

		private void ParseQuests()
		{
			XmlDocument xmlDocument = XmlUtils.OpenXMLDocument(VectorPaths.GeneratorDataDefault, "quests.xml");
			HashSet<string> questNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
			foreach (XmlNode childNode in xmlDocument["Quests"].ChildNodes)
			{
				Quest item = new Quest(childNode);
				_Quests.Add(item);
				questNames.Add(item.Name);
			}
			foreach (XmlNode customNode in CustomProjectCatalog.GetQuestNodes())
			{
				string customName = customNode.Attributes == null || customNode.Attributes["Name"] == null
					? string.Empty : customNode.Attributes["Name"].Value;
				if (string.IsNullOrEmpty(customName) || questNames.Contains(customName))
				{
					DebugUtils.LogFormat("[CustomProject] Skipping duplicate or unnamed quest [{0}]", customName);
					continue;
				}
				try
				{
					Quest customQuest = new Quest(customNode);
					_Quests.Add(customQuest);
					questNames.Add(customQuest.Name);
				}
				catch (System.Exception exception)
				{
					DebugUtils.LogFormat("[CustomProject] Quest [{0}] could not load: {1}", customName, exception.Message);
				}
			}
		}

		private void ParseRewards()
		{
			YamlDocumentNekki yamlDocumentNekki = YamlUtils.OpenYamlFile(VectorPaths.GeneratorDataDefault, "quest_rewards.yaml");
			foreach (Mapping item in yamlDocumentNekki.GetRoot(0))
			{
				Preset preset = Preset.Create(item);
				if (preset == null)
				{
					DebugUtils.Dialog(string.Format("Can't create start items preset from - {0}", item.key), false);
				}
				else
				{
					_Rewards.Add(preset.Name, preset);
				}
			}
		}

		public void GetReward(string p_presetName)
		{
			if (!_Rewards.ContainsKey(p_presetName))
			{
				CustomQuestRewards.Definition custom;
				if (CustomQuestRewards.TryGet(p_presetName, out custom))
				{
					GrantCustomReward(custom);
				}
				return;
			}
			bool flag = false;
			Preset preset = _Rewards[p_presetName];
			for (int i = 0; i < preset.ItemsCount.ValueInt; i++)
			{
				PresetResult presetResult = preset.RunPreset();
				if (StarterPackItem.IsThis(presetResult.Item))
				{
					flag = true;
				}
			}
			DataLocal.Current.Save(false);
			if (flag && Manager.IsEquip)
			{
				Scene<MainScene>.Current.RefreshStarterPacksUI();
			}
		}

		private void GrantCustomReward(CustomQuestRewards.Definition reward)
		{
			string type = reward.Type ?? string.Empty;
			if (type == "Credits" || type == "Premium Currency")
			{
				CounterController.Current.CreateCounterOrSetValue("CurrencyAmount", reward.Amount, "Quests_Temp");
				string presetName = type == "Credits" ? "MoneyReward" : "MoneyPremiumReward";
				Preset preset;
				if (_Rewards.TryGetValue(presetName, out preset)) RunRewardPreset(preset);
			}
			else if (type == "Energy")
			{
				EnergyManager.ChargeEnergyLevel(reward.Amount);
			}
			else if (type == "Card" && !string.IsNullOrEmpty(reward.Reference))
			{
				StringBuffer.AddString("CardName", reward.Reference);
				Preset preset;
				if (_Rewards.TryGetValue("CardReward", out preset)) RunRewardPreset(preset);
			}
			else if (type == "Item" && !string.IsNullOrEmpty(reward.Reference))
			{
				Preset preset;
				if (_Rewards.TryGetValue(reward.Reference, out preset)) RunRewardPreset(preset);
			}
			DataLocal.Current.Save(false);
		}

		private static void RunRewardPreset(Preset preset)
		{
			for (int i = 0; i < preset.ItemsCount.ValueInt; i++) preset.RunPreset();
		}

		public Quest GetQuestByName(string p_name)
		{
			for (int i = 0; i < _Quests.Count; i++)
			{
				if (_Quests[i].Name == p_name)
				{
					return _Quests[i];
				}
			}
			return null;
		}

		public void CheckEvent(TriggerQuestEvent p_event)
		{
			for (int i = 0; i < _Quests.Count; i++)
			{
				_Quests[i].CheckEvent(p_event);
			}
		}

		public void ContinueQuests()
		{
			Dictionary<string, ObscuredInt> counterDictionary = CounterController.Current.GetCounterDictionary("ST_QuestPostpone", false);
			if (counterDictionary == null)
			{
				return;
			}
			counterDictionary = new Dictionary<string, ObscuredInt>(counterDictionary);
			foreach (KeyValuePair<string, ObscuredInt> item in counterDictionary)
			{
				ContinueQuest(item.Key, item.Value);
			}
		}

		private void ContinueQuest(string p_counterName, int p_value)
		{
			string[] array = p_counterName.Split('|');
			Quest questByName = GetQuestByName(array[0]);
			if (questByName == null)
			{
				return;
			}
			if (questByName.IsCompleteState)
			{
				CounterController.Current.RemoveUserCounter(p_counterName, "ST_QuestPostpone");
				return;
			}
			TriggerQuest triggerByName = questByName.GetTriggerByName(array[1]);
			if (triggerByName != null)
			{
				TriggerQuestLoop loopByIndex = triggerByName.GetLoopByIndex(int.Parse(array[2]));
				if (loopByIndex != null)
				{
					loopByIndex.PostponeActionPosition = p_value;
					loopByIndex.ActivateActions();
				}
			}
		}
	}
}
