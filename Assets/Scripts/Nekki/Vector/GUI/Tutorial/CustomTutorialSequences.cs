using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core;
using UnityEngine;

namespace Nekki.Vector.GUI.Tutorial
{
	/// Adapts the editor's friendly tutorial files to Vector's native Steps XML.
	/// Room triggers can play any custom tutorial by its stable ID.
	public static class CustomTutorialSequences
	{
		private static readonly HashSet<string> _SessionRuns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		public static TextAsset Load(string id)
		{
			XmlElement definition = Find(id);
			return definition == null ? null : Build(definition);
		}

		public static bool Play(string id)
		{
			XmlElement definition = Find(id);
			if (definition == null) return false;
			Tutorial tutorial = Tutorial.Current;
			if (tutorial == null)
			{
				GameObject controller = new GameObject("CustomTutorialController");
				tutorial = controller.AddComponent<Tutorial>();
			}
			tutorial.Play(Build(definition));
			return true;
		}

		public static bool TryPlayFirstLaunch(Tutorial tutorial)
		{
			if (tutorial == null || tutorial.Started) return false;
			foreach (XmlElement definition in Definitions())
			{
				if (!Attr(definition, "Type", string.Empty).Equals("First Launch", StringComparison.OrdinalIgnoreCase)) continue;
				string id = Attr(definition, "Id", string.Empty);
				if (string.IsNullOrEmpty(id)) continue;
				string key = "Vector2.CustomTutorial." + id;
				if (PlayerPrefs.GetInt(key, 0) != 0) continue;
				PlayerPrefs.SetInt(key, 1);
				PlayerPrefs.Save();
				tutorial.Play(Build(definition));
				return true;
			}
			return false;
		}

		public static bool PlayMatching(string moment, string reference)
		{
			foreach (XmlElement definition in Definitions())
			{
				string configured = Attr(definition, "Type", string.Empty);
				if (configured.Equals("First Launch", StringComparison.OrdinalIgnoreCase)) configured = "Menu Open";
				if (configured.Equals("Zone Start", StringComparison.OrdinalIgnoreCase)) configured = "Zone Selected";
				if (configured.Equals("Room Start", StringComparison.OrdinalIgnoreCase)) configured = "Floor Start";
				string expected = Attr(definition, "Reference", string.Empty);
				if (!configured.Equals(moment, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(expected) && !expected.Equals(reference, StringComparison.OrdinalIgnoreCase))) continue;
				string id = Attr(definition, "Id", string.Empty);
				if (string.IsNullOrEmpty(id)) continue;
				bool once = Attr(definition, "Once", "1") != "0";
				if (once && !_SessionRuns.Add(id)) continue;
				string key = "Vector2.CustomTutorial." + id;
				if (once && PlayerPrefs.GetInt(key, 0) != 0) continue;
				if (once) { PlayerPrefs.SetInt(key, 1); PlayerPrefs.Save(); }
				Tutorial tutorial = Tutorial.Current;
				if (tutorial == null)
				{
					GameObject controller = new GameObject("CustomTutorialController");
					tutorial = controller.AddComponent<Tutorial>();
				}
				tutorial.Play(Build(definition));
				return true;
			}
			return false;
		}

		private static XmlElement Find(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;
			foreach (XmlElement definition in Definitions())
				if (Attr(definition, "Id", string.Empty).Equals(id, StringComparison.OrdinalIgnoreCase)) return definition;
			return null;
		}

		private static IEnumerable<XmlElement> Definitions()
		{
			string folder = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_tutorials");
			if (!Directory.Exists(folder)) yield break;
			foreach (string path in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories))
			{
				XmlDocument document = new XmlDocument();
				document.XmlResolver = null;
				try { document.Load(path); }
				catch (Exception exception) { Debug.LogWarning("[CustomTutorials] Skipping " + path + ": " + exception.Message); continue; }
				if (document.DocumentElement != null && document.DocumentElement.Name == "CustomTutorial") yield return document.DocumentElement;
			}
		}

		private static TextAsset Build(XmlElement definition)
		{
			XmlDocument document = new XmlDocument();
			XmlElement steps = document.CreateElement("Steps");
			document.AppendChild(steps);
			XmlNodeList authoredSteps = definition.SelectNodes("Step");
			if (authoredSteps == null || authoredSteps.Count == 0)
				AppendNotification(document, steps, Attr(definition, "Message", Attr(definition, "Description", Attr(definition, "Name", string.Empty))), Attr(definition, "Reference", string.Empty));
			else
				foreach (XmlNode authored in authoredSteps)
					AppendNotification(document, steps, Attr(authored as XmlElement, "Message", string.Empty), Attr(authored as XmlElement, "Portrait", string.Empty));
			steps.AppendChild(document.CreateElement("End"));
			return new TextAsset(document.OuterXml);
		}

		private static void AppendNotification(XmlDocument document, XmlElement steps, string text, string portrait)
		{
			if (string.IsNullOrEmpty(text)) return;
			XmlElement notification = document.CreateElement("Notification");
			notification.SetAttribute("Text", text);
			notification.SetAttribute("Portrait", portrait ?? string.Empty);
			notification.SetAttribute("Orientation", "Left");
			notification.SetAttribute("HideBy", "TimeBlockClicks");
			steps.AppendChild(notification);
		}

		private static string Attr(XmlElement node, string name, string fallback) { string value = node.GetAttribute(name); return string.IsNullOrEmpty(value) ? fallback : value.Trim(); }
	}
}
