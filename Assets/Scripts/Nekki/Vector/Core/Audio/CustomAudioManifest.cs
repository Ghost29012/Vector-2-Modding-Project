using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core.GameManagement;
using UnityEngine;

namespace Nekki.Vector.Core.Audio
{
	/// Select custom music for each context. CustomAudioCatalog handles the files.
	public static class CustomAudioManifest
	{
		private sealed class Track
		{
			public string Id;
			public int Weight;
			public int MinFloor;
			public int MaxFloor;
		}

		private sealed class Pool
		{
			public string Scope;
			public string Reference;
			public string Ambient;
			public float AmbientVolume;
			public readonly List<Track> Tracks = new List<Track>();
		}

		private static readonly List<Pool> _Pools = new List<Pool>();
		private static bool _Loaded;
		private static string _LastTrack = string.Empty;

		public static void Reload() { _Loaded = false; _Pools.Clear(); }

		public static bool TrySelectForCurrentZone(out string track, out string ambient, out float ambientVolume)
		{
			track = string.Empty; ambient = string.Empty; ambientVolume = 0.5f;
			EnsureLoaded();
			if (!ZoneManager.IsCustomZoneActive) return false;
			CustomProjectCatalog.ZoneDefinition zone;
			if (!CustomProjectCatalog.TryGetZone(ZoneManager.CurrentZoneId, out zone)) return false;
			Pool pool = FindPool("Zone", zone.Id) ?? FindPool("Chapter", zone.ChapterId);
			if (pool == null) return false;
			ambient = pool.Ambient;
			ambientVolume = pool.AmbientVolume;
			int floor = Math.Max(0, RunMainController.CurrentFloor);
			List<Track> eligible = pool.Tracks.FindAll(item =>
				(item.MinFloor <= 0 || floor >= item.MinFloor) && (item.MaxFloor <= 0 || floor <= item.MaxFloor));
			if (eligible.Count == 0) return !string.IsNullOrEmpty(ambient);
			List<Track> choices = eligible.Count > 1 ? eligible.FindAll(item => !string.Equals(item.Id, _LastTrack, StringComparison.OrdinalIgnoreCase)) : eligible;
			if (choices.Count == 0) choices = eligible;
			int total = 0;
			foreach (Track item in choices) total += Math.Max(1, item.Weight);
			int roll = UnityEngine.Random.Range(0, total);
			foreach (Track item in choices)
			{
				roll -= Math.Max(1, item.Weight);
				if (roll >= 0) continue;
				track = item.Id;
				_LastTrack = track;
				break;
			}
			return !string.IsNullOrEmpty(track) || !string.IsNullOrEmpty(ambient);
		}

		private static Pool FindPool(string scope, string reference)
		{
			if (string.IsNullOrEmpty(reference)) return null;
			return _Pools.Find(item => string.Equals(item.Scope, scope, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(item.Reference, reference, StringComparison.OrdinalIgnoreCase));
		}

		private static void EnsureLoaded()
		{
			if (_Loaded) return;
			_Loaded = true;
			string path = Path.Combine(CustomProjectCatalog.StorageRoot, "custom_audio", "audio_manifest.xml");
			if (!File.Exists(path)) return;
			try
			{
				XmlDocument document = new XmlDocument(); document.XmlResolver = null; document.Load(path);
				foreach (XmlNode node in document.SelectNodes("/CustomAudio/MusicPools/Pool"))
				{
					Pool pool = new Pool();
					pool.Scope = Attr(node, "Scope"); pool.Reference = Attr(node, "Reference"); pool.Ambient = Attr(node, "Ambient");
					float volume; pool.AmbientVolume = float.TryParse(Attr(node, "AmbientVolume"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out volume) ? Mathf.Clamp01(volume) : 0.5f;
					if (string.IsNullOrEmpty(pool.Reference)) continue;
					foreach (XmlNode child in node.SelectNodes("Track"))
					{
						Track item = new Track(); item.Id = Attr(child, "Id");
						if (string.IsNullOrEmpty(item.Id)) continue;
						item.Weight = IntAttr(child, "Weight", 1); item.MinFloor = IntAttr(child, "MinFloor", 0); item.MaxFloor = IntAttr(child, "MaxFloor", 0);
						pool.Tracks.Add(item);
					}
					_Pools.Add(pool);
				}
			}
			catch (Exception e) { Debug.LogWarning("[CustomAudio] Ignoring invalid manifest: " + e.Message); }
		}

		private static string Attr(XmlNode node, string name) { return node.Attributes != null && node.Attributes[name] != null ? node.Attributes[name].Value : string.Empty; }
		private static int IntAttr(XmlNode node, string name, int fallback) { int value; return int.TryParse(Attr(node, name), out value) ? value : fallback; }
	}
}
