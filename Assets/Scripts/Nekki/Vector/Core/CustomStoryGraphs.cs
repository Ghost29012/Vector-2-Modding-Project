using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core.Quest;
using Nekki.Vector.Core.Trigger.Events;
using Nekki.Vector.Core.Variables;
using Nekki.Vector.GUI.Common;
using Nekki.Vector.GUI.Dialogs;
using UnityEngine;

namespace Nekki.Vector.Core
{
	/// Runs Project Manager story graphs using the supported node types.
	public static class CustomStoryGraphs
	{
		private const int MaxSteps = 100;
		private static readonly HashSet<string> _Running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public static void Fire(string moment, string reference)
		{
			string folder = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_story");
			if (!Directory.Exists(folder)) return;
			foreach (string file in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories))
			{
				try
				{
					XmlDocument doc = new XmlDocument(); doc.Load(file);
					foreach (XmlNode graph in doc.SelectNodes("//StoryGraph"))
						if (Matches(Attr(graph, "Trigger"), Attr(graph, "Reference"), moment, reference)) Start(graph);
				}
				catch (Exception e) { Debug.LogWarning("[CustomStory] Could not load " + file + ": " + e.Message); }
			}
		}

		public static bool Play(string id)
		{
			if (string.IsNullOrEmpty(id)) return false;
			string folder = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_story");
			if (!Directory.Exists(folder)) return false;
			foreach (string file in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories))
			{
				try
				{
					XmlDocument doc = new XmlDocument(); doc.XmlResolver = null; doc.Load(file);
					foreach (XmlNode graph in doc.SelectNodes("//StoryGraph"))
					{
						if (!Attr(graph, "Id").Equals(id, StringComparison.OrdinalIgnoreCase)) continue;
						Start(graph);
						return true;
					}
				}
				catch (Exception e) { Debug.LogWarning("[CustomStory] Could not load " + file + ": " + e.Message); }
			}
			return false;
		}

		private static void Start(XmlNode graph)
		{
			string id = Attr(graph, "Id");
			if (string.IsNullOrEmpty(id) || _Running.Contains(id)) return;
			string onceKey = "Vector2.CustomStory." + id;
			if (Bool(graph, "Once", true) && PlayerPrefs.GetInt(onceKey, 0) != 0) return;
			_Running.Add(id);
			if (Bool(graph, "Once", true)) { PlayerPrefs.SetInt(onceKey, 1); PlayerPrefs.Save(); }
			Run(graph, Attr(graph, "Start"), 0);
		}

		private static void Run(XmlNode graph, string nodeId, int steps)
		{
			string graphId = Attr(graph, "Id");
			if (steps >= MaxSteps || string.IsNullOrEmpty(nodeId)) { _Running.Remove(graphId); return; }
			XmlNode node = graph.SelectSingleNode("Node[@Id=" + XPathLiteral(nodeId) + "]");
			if (node == null) { _Running.Remove(graphId); return; }
			string type = Attr(node, "Type");
			string next = Attr(node, "Next");
			Action continueStory = delegate { Run(graph, next, steps + 1); };

			if (type.Equals("Dialogue", StringComparison.OrdinalIgnoreCase))
			{
				CustomProjectCatalog.Dialogue dialogue;
				if (!CustomProjectCatalog.TryGetDialogue(Attr(node, "Dialogue"), out dialogue)) { continueStory(); return; }
				string title = dialogue.Title, image = dialogue.Image;
				CustomProjectCatalog.Speaker speaker;
				if (CustomProjectCatalog.TryGetSpeaker(dialogue.SpeakerId, out speaker)) { if (string.IsNullOrEmpty(title)) title = speaker.Name; if (string.IsNullOrEmpty(image)) image = speaker.Portrait; }
				DialogNotificationManager.ShowQuestTalkingDialog(title, dialogue.Text, string.IsNullOrEmpty(dialogue.Button) ? "Continue" : dialogue.Button, image, continueStory);
				return;
			}
			if (type.Equals("Entrance", StringComparison.OrdinalIgnoreCase))
			{
				CustomProjectCatalog.Speaker speaker;
				string speakerId = Attr(node, "Speaker");
				string title = speakerId, image = string.Empty;
				if (CustomProjectCatalog.TryGetSpeaker(speakerId, out speaker)) { title = speaker.Name; image = speaker.Portrait; }
				DialogNotificationManager.ShowQuestTalkingDialog(title, Attr(node, "Text"), "Continue", image, continueStory);
				return;
			}
			if (type.Equals("Choice", StringComparison.OrdinalIgnoreCase))
			{
				List<DialogButtonData> buttons = new List<DialogButtonData>();
				foreach (XmlNode choice in node.SelectNodes("Choice"))
				{
					string target = Attr(choice, "Next"); string label = Attr(choice, "Text");
					buttons.Add(new DialogButtonData(delegate(BaseDialog dialog) { dialog.Dismiss(); Run(graph, target, steps + 1); }, label, ButtonUI.Type.Blue, false));
				}
				if (buttons.Count == 0) { continueStory(); return; }
				DialogNotificationManager.ShowQuestTalkingDialog(Attr(node, "Title"), Attr(node, "Text"), Attr(node, "Image"), buttons);
				return;
			}
			if (type.Equals("SetFlag", StringComparison.OrdinalIgnoreCase))
			{
				PlayerPrefs.SetString("Vector2.StoryFlag." + Attr(node, "Flag"), Attr(node, "Value")); PlayerPrefs.Save(); continueStory(); return;
			}
			if (type.Equals("Branch", StringComparison.OrdinalIgnoreCase))
			{
				string actual = PlayerPrefs.GetString("Vector2.StoryFlag." + Attr(node, "Flag"), string.Empty);
				Run(graph, actual == Attr(node, "Equals") ? Attr(node, "True") : Attr(node, "False"), steps + 1); return;
			}
			if (type.Equals("QuestSignal", StringComparison.OrdinalIgnoreCase))
			{
				if (QuestManager.Current != null) QuestManager.Current.CheckEvent(TQE_OnCall.CalledByTriggerEvent(Variable.CreateVariable(Attr(node, "Signal"))));
				continueStory(); return;
			}
			if (type.Equals("SceneEvent", StringComparison.OrdinalIgnoreCase) || type.Equals("Cutscene", StringComparison.OrdinalIgnoreCase)) { CustomContentEvents.CustomEvent(Attr(node, "Event")); continueStory(); return; }
			_Running.Remove(graphId);
		}

		private static bool Matches(string configuredMoment, string configuredReference, string moment, string reference)
		{
			return !string.IsNullOrEmpty(configuredMoment) && !configuredMoment.Equals("Manual", StringComparison.OrdinalIgnoreCase)
				&& configuredMoment.Equals(moment, StringComparison.OrdinalIgnoreCase)
				&& (string.IsNullOrEmpty(configuredReference) || configuredReference.Equals(reference, StringComparison.OrdinalIgnoreCase));
		}
		private static string Attr(XmlNode node, string name) { return node.Attributes[name] == null ? string.Empty : node.Attributes[name].Value; }
		private static bool Bool(XmlNode node, string name, bool fallback) { string value = Attr(node, name); return string.IsNullOrEmpty(value) ? fallback : value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase); }
		private static string XPathLiteral(string value) { return value.IndexOf('\'') < 0 ? "'" + value + "'" : "\"" + value.Replace("\"", string.Empty) + "\""; }
	}
}
