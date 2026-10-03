using System;
using System.Collections.Generic;
using System.IO;

namespace Nekki.Vector.Core.Audio
{
	/// Maps friendly file names from custom_audio to stable sound IDs. A creator
	/// can reference "alarm" in game data when the project contains alarm.ogg.
	public static class CustomAudioCatalog
	{
		public struct Entry
		{
			public string Id;
			public string Path;
			public Entry(string id, string path) { Id = id; Path = path; }
		}

		private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			".wav", ".mp3", ".ogg", ".aif", ".aiff", ".m4a"
		};

		public static IEnumerable<Entry> Entries(string storageRoot)
		{
			string root = Path.Combine(storageRoot, "custom_audio");
			if (!Directory.Exists(root)) yield break;
			foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
			{
				if (!Extensions.Contains(Path.GetExtension(path))) continue;
				string id = Path.GetFileNameWithoutExtension(path);
				if (!string.IsNullOrEmpty(id)) yield return new Entry(id, Path.GetFullPath(path));
			}
		}
	}
}
