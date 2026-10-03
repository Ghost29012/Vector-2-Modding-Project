using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Nekki.Vector.Core.Animation
{
	public static class AnimationOverrideLoader
	{
		private const int SkeletonNodes = 46;

		public static void Load()
		{
			string storage = !string.IsNullOrEmpty(VectorPaths.CurrentStorage) ? VectorPaths.CurrentStorage : Application.persistentDataPath;
			string root = Path.Combine(storage, "custom_tricks", "animation_overrides");
			Directory.CreateDirectory(root);
			string[] manifests = Directory.GetFiles(root, "override.xml", SearchOption.AllDirectories);
			Array.Sort(manifests, StringComparer.OrdinalIgnoreCase);
			HashSet<string> appliedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string manifestPath in manifests)
			{
				try
				{
					XmlDocument document = new XmlDocument();
					document.Load(manifestPath);
					XmlElement source = document.DocumentElement;
					if (source == null || source.Name != "AnimationOverride") throw new Exception("root must be <AnimationOverride>");
					if (source.HasAttribute("Enabled") && source.GetAttribute("Enabled") == "0") continue;
					string targetName = Required(source, "Target");
					if (appliedTargets.Contains(targetName)) throw new Exception("another enabled override already targets " + targetName);
					AnimationInfo target;
					if (!Animations.Animation.TryGetValue(targetName, out target)) throw new Exception("unknown animation target: " + targetName);
					string package = Path.GetFullPath(Path.GetDirectoryName(manifestPath));
					string binaryPath = Path.GetFullPath(Path.Combine(package, Required(source, "FileName")));
					if (!binaryPath.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new Exception("FileName must stay inside its override package");
					int frames = ValidateBinary(binaryPath);
					int requiredFrame = Math.Max(target.FirstFrame, target.DeclaredEndFrame);
					if (frames <= requiredFrame) throw new Exception("replacement has " + frames + " frames; " + targetName + " requires frame " + requiredFrame);
					target.OverrideBinary(binaryPath);
					appliedTargets.Add(targetName);
					Debug.Log("[AnimationOverrides] " + targetName + " <- " + binaryPath + " (" + frames + " frames)");
				}
				catch (Exception e) { Debug.LogWarning("[AnimationOverrides] Skipped " + manifestPath + ": " + e.Message); }
			}
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
					for (int valueIndex = 0; valueIndex < nodes * 3; valueIndex++)
					{
						float value = reader.ReadSingle();
						if (float.IsNaN(value) || float.IsInfinity(value)) throw new Exception("non-finite coordinate at frame " + frame);
					}
				}
				if (reader.BaseStream.Position != reader.BaseStream.Length) throw new Exception("unexpected trailing data");
				return frames;
			}
		}

		private static string Required(XmlElement node, string name)
		{
			string value = node.GetAttribute(name);
			if (string.IsNullOrEmpty(value)) throw new Exception("missing " + name);
			return value;
		}
	}
}
