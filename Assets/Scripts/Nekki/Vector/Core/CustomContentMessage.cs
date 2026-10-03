using System;

namespace Nekki.Vector.Core
{
	public enum CustomContentMessageKind
	{
		Dialogue,
		Tutorial,
		Story,
		Zone,
		CustomEvent
	}

	/// Pure parser for the stable room/quest message contract shared with the editor.
	public struct CustomContentMessage
	{
		public CustomContentMessageKind Kind;
		public string Reference;

		public static bool TryParse(string value, out CustomContentMessage result)
		{
			result = new CustomContentMessage();
			if (string.IsNullOrEmpty(value)) return false;
			string[] prefixes = { "Content.Dialogue:", "Content.Tutorial:", "Content.Story:", "Content.Zone:", "Content.CustomEvent:" };
			CustomContentMessageKind[] kinds = { CustomContentMessageKind.Dialogue, CustomContentMessageKind.Tutorial, CustomContentMessageKind.Story, CustomContentMessageKind.Zone, CustomContentMessageKind.CustomEvent };
			for (int index = 0; index < prefixes.Length; index++)
			{
				if (!value.StartsWith(prefixes[index], StringComparison.OrdinalIgnoreCase)) continue;
				string reference = value.Substring(prefixes[index].Length).Trim();
				if (reference.Length == 0) return false;
				result.Kind = kinds[index];
				result.Reference = reference;
				return true;
			}
			return false;
		}
	}
}
