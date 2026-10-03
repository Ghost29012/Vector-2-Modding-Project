using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Nekki.Vector.Core.GameManagement;
using UnityEngine;

namespace Nekki.Vector.Core.Animation
{
	public static class CustomTrickLoader
	{
		private const int SkeletonNodes = 46;
		private static string _BuiltInBinaryPath;
		private static readonly Dictionary<string, string> _CustomPackages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, long> _PackageStamps = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> _StuntGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> _CardNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> _RunImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public static bool IsCustom(string animationName) { return !string.IsNullOrEmpty(animationName) && _CustomPackages.ContainsKey(animationName); }

		public static bool TryGetStuntGroup(string animationName, out string group)
		{
			return _StuntGroups.TryGetValue(animationName ?? string.Empty, out group);
		}

		public static bool TryGetCardName(string animationName, out string cardName)
		{
			return _CardNames.TryGetValue(animationName ?? string.Empty, out cardName);
		}

		public static bool TryGetRunImage(string animationName, out string runImage)
		{
			return _RunImages.TryGetValue(animationName ?? string.Empty, out runImage);
		}

		public static void Load(string builtInBinaryPath)
		{
			_BuiltInBinaryPath = builtInBinaryPath;
			string storage = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
			foreach (string root in SearchRoots(storage))
			{
				Directory.CreateDirectory(root);
				foreach (string manifestPath in Directory.GetFiles(root, "trick.xml", SearchOption.AllDirectories))
				{
					try { LoadPackage(manifestPath, builtInBinaryPath); }
					catch (Exception e) { Debug.LogWarning("[CustomTricks] Skipped " + manifestPath + ": " + e.Message); }
				}
			}
		}

		public static bool EnsureLoaded(string animationName)
		{
			if (string.IsNullOrEmpty(animationName)) return true;
			string knownManifest;
			if (_CustomPackages.TryGetValue(animationName, out knownManifest))
			{
				long knownStamp; _PackageStamps.TryGetValue(animationName, out knownStamp);
				if (PackageStamp(knownManifest) != knownStamp) LoadPackage(knownManifest, _BuiltInBinaryPath);
				return true;
			}
			if (Animations.Animation.ContainsKey(animationName)) return true;
			if (string.IsNullOrEmpty(_BuiltInBinaryPath)) return false;
			string storage = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
			foreach (string root in SearchRoots(storage))
			{
				if (!Directory.Exists(root)) continue;
				foreach (string manifestPath in Directory.GetFiles(root, "trick.xml", SearchOption.AllDirectories))
				{
					try
					{
						XmlDocument manifest = new XmlDocument();
						manifest.Load(manifestPath);
						XmlElement source = manifest.DocumentElement;
						if (source == null || source.Name != "CustomTrick" || Required(source, "Name") != animationName) continue;
						LoadPackage(manifestPath, _BuiltInBinaryPath);
						Debug.Log("[CustomTricks] Hot-loaded " + animationName);
						return true;
					}
					catch (Exception e) { Debug.LogWarning("[CustomTricks] Hot-load failed " + manifestPath + ": " + e.Message); return false; }
				}
			}
			return false;
		}

		public static void Reload()
		{
			AnimationGroup tricks = AnimationGroup.GetGroup("TrickGroup");
			foreach (string name in new List<string>(_CustomPackages.Keys))
			{
				Animations.Animation.Remove(name);
				if (tricks != null) tricks.RemoveReactions(name);
			}
			_CustomPackages.Clear();
			_PackageStamps.Clear();
			_StuntGroups.Clear();
			_CardNames.Clear();
			_RunImages.Clear();
			if (!string.IsNullOrEmpty(_BuiltInBinaryPath)) Load(_BuiltInBinaryPath);
		}

		public static string ActiveTricksRoot(string storage)
		{
			if (ZoneManager.IsCustomZoneActive)
			{
				CustomProjectCatalog.ZoneDefinition zone;
				if (CustomProjectCatalog.TryGetZone(ZoneManager.CurrentZoneId, out zone))
					return CustomProjectCatalog.ResolveProjectFolder(zone.TricksPath, "custom_tricks");
			}
			return Path.Combine(storage, "custom_tricks");
		}

		public static List<string> SearchRoots(string storage)
		{
			return CustomTrickSearchRoots.Combine(Path.Combine(storage, "custom_tricks"), ActiveTricksRoot(storage));
		}

		private static void LoadPackage(string manifestPath, string builtInBinaryPath)
		{
			XmlDocument manifest = new XmlDocument();
			manifest.Load(manifestPath);
			XmlElement source = manifest.DocumentElement;
			if (source == null || source.Name != "CustomTrick") throw new Exception("root must be <CustomTrick>");

			string name = Required(source, "Name");
			if (Animations.Animation.ContainsKey(name))
			{
				if (!_CustomPackages.ContainsKey(name)) throw new Exception("animation name already exists: " + name);
				Animations.Animation.Remove(name);
				AnimationGroup oldGroup = AnimationGroup.GetGroup("TrickGroup");
				if (oldGroup != null) oldGroup.RemoveReactions(name);
			}
			string binaryPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath), Required(source, "FileName")));
			if (!binaryPath.StartsWith(Path.GetFullPath(Path.GetDirectoryName(manifestPath)) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
				throw new Exception("FileName must stay inside its trick package");
			int frames = ValidateBinary(binaryPath);

			XmlDocument generated = new XmlDocument();
			XmlElement move = generated.CreateElement(name);
			generated.AppendChild(move);
			Set(move, "Trick", "1");
			Set(move, "Type", Get(source, "Type", "3"));
			Set(move, "DeltaDetectorH", Get(source, "DeltaDetectorH", "20"));
			Set(move, "DeltaDetectorV", Get(source, "DeltaDetectorV", "40"));
			Set(move, "FileName", binaryPath);
			Set(move, "FirstFrame", Get(source, "FirstFrame", "0"));
			Set(move, "EndFrame", Get(source, "EndFrame", (frames - 1).ToString()));
			Set(move, "PivotNode", Get(source, "PivotNode", "DetectorH"));
			CopyOptional(source, move, "Mirror", "VelocityX", "VelocityY", "Gravity", "Binding", "AutoPositionDetectorH", "AutoPositionDetectorV", "LandingPositionDetectorH", "LandingPositionDetectorV", "PlatformAnticipationFrames");

			bool hasIntervals = false;
			foreach (XmlNode child in source.ChildNodes)
			{
				if (child.Name != "Interval") continue;
				move.AppendChild(generated.ImportNode(child, true));
				hasIntervals = true;
			}
			if (!hasIntervals) AddDefaultInterval(generated, move, source, frames);

			AnimationTrickInfo info = new AnimationTrickInfo(move);
			info.Load(builtInBinaryPath);
			foreach (XmlNode child in move.ChildNodes)
			{
				if (child.Name == "Interval")
				{
					info.Intervals.Add(new AnimationInterval(child));
				}
			}
			Animations.Animation.Add(name, info);
			_CustomPackages[name] = manifestPath;
			_PackageStamps[name] = PackageStamp(manifestPath);
			_CardNames[name] = Nekki.Vector.Core.Runners.CustomStuntPlacement.CardNameFor(source);
			_RunImages[name] = Nekki.Vector.Core.Runners.CustomStuntPlacement.RunImageFor(source);
			string stuntGroup = Get(source, "StuntGroup", "Group1-1");
			if (string.IsNullOrEmpty(stuntGroup) || !stuntGroup.StartsWith("Group", StringComparison.OrdinalIgnoreCase))
			{
				stuntGroup = "Group1-1";
			}
			_StuntGroups[name] = stuntGroup;

			AnimationGroup tricks = AnimationGroup.GetGroup("TrickGroup");
			if (tricks == null) throw new Exception("TrickGroup is unavailable");
			XmlElement reaction = generated.CreateElement(name);
			Set(reaction, "FirstFrame", Get(source, "EntryFrame", "0"));
			Set(reaction, "Priority", Get(source, "Priority", "10"));
			Set(reaction, "OnEndTrigger", "1");
			Set(reaction, "AreaName", name);
			if (Get(source, "SafeH", "0") == "1") Set(reaction, "SafeH", "1");
			if (Get(source, "SafeV", "0") == "1") Set(reaction, "SafeV", "1");
			tricks.AddReaction(new AnimationReaction(reaction));
			Debug.Log("[CustomTricks] Loaded " + name + " (" + frames + " frames) from " + manifestPath);
		}

		private static long PackageStamp(string manifestPath)
		{
			long stamp = File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath).Ticks : 0;
			try { XmlDocument document = new XmlDocument(); document.Load(manifestPath); string binary = Path.Combine(Path.GetDirectoryName(manifestPath), Required(document.DocumentElement, "FileName")); if (File.Exists(binary)) stamp = Math.Max(stamp, File.GetLastWriteTimeUtc(binary).Ticks); } catch { }
			return stamp;
		}

		private static void AddDefaultInterval(XmlDocument doc, XmlElement move, XmlElement source, int frames)
		{
			int safeStart = ParseFrame(source, "SafeStart", 0, frames);
			// ControllerAnimation's displayed/current frame tops out one frame
			// below the binary's declared EndFrame. Put OnEnd on the final
			// reachable frame or a forced custom trick will hold its last pose.
			int lastReachableFrame = Math.Max(0, frames - 2);
			int safeEnd = Math.Min(ParseFrame(source, "SafeEnd", lastReachableFrame, frames), lastReachableFrame);
			safeStart = Math.Min(safeStart, safeEnd);
			XmlElement interval = doc.CreateElement("Interval");
			Set(interval, "Start", safeStart.ToString()); Set(interval, "End", safeEnd.ToString()); Set(interval, "Safe", "1");
			XmlElement onEnd = doc.CreateElement("OnEnd");
			XmlElement reactions = doc.CreateElement("Reactions");
			XmlElement exit = doc.CreateElement(Get(source, "ExitAnimation", "RunForward"));
			Set(exit, "FirstFrame", Get(source, "ExitFrame", "1")); Set(exit, "Priority", "1");
			reactions.AppendChild(exit); onEnd.AppendChild(reactions); interval.AppendChild(onEnd); move.AppendChild(interval);
		}

		private static int ValidateBinary(string path)
		{
			if (!File.Exists(path)) throw new Exception("missing bytes file: " + Path.GetFileName(path));
			using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
			{
				int frames = reader.ReadInt32();
				if (frames <= 0 || frames > 10000) throw new Exception("invalid frame count");
				for (int frame = 0; frame < frames; frame++)
				{
					reader.ReadByte();
					int nodes = reader.ReadInt32();
					if (nodes != SkeletonNodes) throw new Exception("frame " + frame + " has " + nodes + " nodes; expected " + SkeletonNodes);
					for (int node = 0; node < nodes * 3; node++)
					{
						float value = reader.ReadSingle();
						if (float.IsNaN(value) || float.IsInfinity(value)) throw new Exception("non-finite coordinate at frame " + frame);
					}
				}
				if (reader.BaseStream.Position != reader.BaseStream.Length) throw new Exception("unexpected trailing data");
				return frames;
			}
		}

		private static int ParseFrame(XmlElement node, string name, int fallback, int frames)
		{
			int value; if (!int.TryParse(Get(node, name, fallback.ToString()), out value)) value = fallback;
			return Math.Max(0, Math.Min(frames - 1, value));
		}
		private static string Required(XmlElement node, string name) { string value = node.GetAttribute(name); if (string.IsNullOrEmpty(value)) throw new Exception("missing " + name); return value; }
		private static string Get(XmlElement node, string name, string fallback) { string value = node.GetAttribute(name); return string.IsNullOrEmpty(value) ? fallback : value; }
		private static void Set(XmlElement node, string name, string value) { node.SetAttribute(name, value); }
		private static void CopyOptional(XmlElement source, XmlElement target, params string[] names) { foreach (string name in names) if (source.HasAttribute(name)) target.SetAttribute(name, source.GetAttribute(name)); }
	}
}
