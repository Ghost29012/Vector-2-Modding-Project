using System;
using System.Collections.Generic;
using System.IO;
using Nekki.Vector.Core.GameManagement;
using Nekki.Vector.Core.Quest;
using Nekki.Vector.Core.Trigger.Events;
using Nekki.Vector.Core.Variables;
using Nekki.Vector.Core.Scripts;
using Nekki.Vector.GUI.Dialogs;
using Nekki.Vector.GUI.Tutorial;
using UnityEngine;

namespace Nekki.Vector.Core
{
	/// Starts custom dialogue and tutorials from room and chapter events.
	public static class CustomContentEvents
	{
		private static readonly HashSet<string> _SessionRuns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static string _LastChapter = string.Empty;
		private static readonly Dictionary<string, List<CustomAnimationSprite>> _Visuals = new Dictionary<string, List<CustomAnimationSprite>>(StringComparer.OrdinalIgnoreCase);

		public static void RegisterVisual(string group, CustomAnimationSprite visual)
		{
			if (string.IsNullOrEmpty(group) || visual == null) return;
			List<CustomAnimationSprite> items;
			if (!_Visuals.TryGetValue(group, out items)) _Visuals[group] = items = new List<CustomAnimationSprite>();
			items.RemoveAll(item => item == null);
			if (!items.Contains(visual)) items.Add(visual);
		}

		private static bool SwitchVisual(string value)
		{
			string[] parts = value.Split(new[] { ':' }, 3);
			if (parts.Length != 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2])) return false;
			string manifest = parts[2];
			if (Path.GetFileName(manifest) != manifest || !manifest.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return false;
			string storage = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
			string textures = Path.Combine(storage, "custom_textures");
			if (!File.Exists(Path.Combine(textures, manifest))) return false;
			List<CustomAnimationSprite> items;
			if (!_Visuals.TryGetValue(parts[1], out items)) return false;
			items.RemoveAll(item => item == null);
			bool changed = false;
			foreach (CustomAnimationSprite item in items) changed |= item.TryChangeSequence(manifest);
			return changed;
		}

		public static void MenuReady()
		{
			Fire("Menu Open", string.Empty);
			FireChapterStartIfChanged();
		}

		public static void ZoneSelected(string zoneId)
		{
			Fire("Zone Selected", zoneId);
			FireChapterStartIfChanged();
		}

		private static void FireChapterStartIfChanged()
		{
			CustomProjectCatalog.Chapter chapter;
			if (!CustomProjectCatalog.TryGetCurrentChapter(out chapter) || string.IsNullOrEmpty(chapter.Id)) return;
			if (_LastChapter.Equals(chapter.Id, StringComparison.OrdinalIgnoreCase)) return;
			_LastChapter = chapter.Id;
			Fire("Chapter Start", chapter.Id);
		}

		public static void FloorStarted(int floor) { Fire("Floor Start", floor.ToString()); }
		public static void CustomEvent(string eventName) { Fire("Custom Event", eventName ?? string.Empty); }

		/// Handles Content.* messages after ExecuteCall sends the quest event.
		/// Calling QuestManager here would dispatch the same message again.
		public static bool DispatchTriggerMessage(string value)
		{
			if (!string.IsNullOrEmpty(value) && value.StartsWith("Content.Visual:", StringComparison.OrdinalIgnoreCase))
				return SwitchVisual(value.Substring("Content.".Length));
			CustomContentMessage message;
			if (!CustomContentMessage.TryParse(value, out message)) return false;
			switch (message.Kind)
			{
				case CustomContentMessageKind.Dialogue: return PlayDialogue(message.Reference);
				case CustomContentMessageKind.Tutorial: return CustomTutorialSequences.Play(message.Reference);
				case CustomContentMessageKind.Story: return CustomStoryGraphs.Play(message.Reference);
				case CustomContentMessageKind.Zone:
					if (!ZoneManager.SetCurrentCustomZone(message.Reference)) return false;
					ZoneSelected(message.Reference);
					return true;
				case CustomContentMessageKind.CustomEvent:
					FireContent("Custom Event", message.Reference);
					return true;
			}
			return false;
		}

		private static void Fire(string moment, string reference)
		{
			string signal = "Content." + moment.Replace(" ", string.Empty);
			if (!string.IsNullOrEmpty(reference)) signal += ":" + reference;
			if (QuestManager.Current != null)
				QuestManager.Current.CheckEvent(TQE_OnCall.CalledByTriggerEvent(Variable.CreateVariable(signal)));
			FireContent(moment, reference);
		}

		private static void FireContent(string moment, string reference)
		{
			foreach (CustomProjectCatalog.Dialogue dialogue in CustomProjectCatalog.Dialogues)
			{
				if (Matches(dialogue.Trigger, dialogue.Reference, moment, reference)) ShowDialogue(dialogue, true);
			}
			CustomTutorialSequences.PlayMatching(moment, reference);
			CustomStoryGraphs.Fire(moment, reference);
		}

		private static bool PlayDialogue(string id)
		{
			CustomProjectCatalog.Dialogue dialogue;
			if (!CustomProjectCatalog.TryGetDialogue(id, out dialogue)) return false;
			return ShowDialogue(dialogue, false);
		}

		private static bool ShowDialogue(CustomProjectCatalog.Dialogue dialogue, bool honorOnce)
		{
			if (dialogue == null || (honorOnce && !CanRun("Dialogue", dialogue.Id, dialogue.Once))) return false;
			string title = dialogue.Title;
			string image = dialogue.Image;
			CustomProjectCatalog.Speaker speaker;
			if (CustomProjectCatalog.TryGetSpeaker(dialogue.SpeakerId, out speaker))
			{
				if (string.IsNullOrEmpty(title)) title = speaker.Name;
				if (string.IsNullOrEmpty(image)) image = speaker.Portrait;
			}
			DialogNotificationManager.ShowQuestTalkingDialog(title, dialogue.Text,
				string.IsNullOrEmpty(dialogue.Button) ? "Continue" : dialogue.Button, image, delegate { }, 0);
			return true;
		}

		private static bool Matches(string configuredMoment, string configuredReference, string moment, string reference)
		{
			if (string.IsNullOrEmpty(configuredMoment) || configuredMoment.Equals("Manual", StringComparison.OrdinalIgnoreCase)) return false;
			if (configuredMoment.Equals("First Launch", StringComparison.OrdinalIgnoreCase)) configuredMoment = "Menu Open";
			if (configuredMoment.Equals("Zone Start", StringComparison.OrdinalIgnoreCase)) configuredMoment = "Zone Selected";
			if (configuredMoment.Equals("Room Start", StringComparison.OrdinalIgnoreCase)) configuredMoment = "Floor Start";
			return configuredMoment.Equals(moment, StringComparison.OrdinalIgnoreCase)
				&& (string.IsNullOrEmpty(configuredReference) || configuredReference.Equals(reference, StringComparison.OrdinalIgnoreCase));
		}

		private static bool CanRun(string kind, string id, bool once)
		{
			string session = kind + ":" + id;
			if (!once) return true;
			if (!_SessionRuns.Add(session)) return false;
			string key = "Vector2.CustomContent." + session;
			if (PlayerPrefs.GetInt(key, 0) != 0) return false;
			PlayerPrefs.SetInt(key, 1);
			PlayerPrefs.Save();
			return true;
		}
	}
}
