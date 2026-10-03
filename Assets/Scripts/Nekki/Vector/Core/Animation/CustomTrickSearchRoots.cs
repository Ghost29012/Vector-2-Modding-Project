using System;
using System.Collections.Generic;
using System.IO;

namespace Nekki.Vector.Core.Animation
{
    internal static class CustomTrickSearchRoots
    {
        internal static List<string> Combine(string globalRoot, string zoneRoot)
        {
            string global = Path.GetFullPath(globalRoot).TrimEnd(Path.DirectorySeparatorChar);
            var roots = new List<string> { global };
            if (string.IsNullOrEmpty(zoneRoot)) return roots;

            string zone = Path.GetFullPath(zoneRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (!zone.Equals(global, StringComparison.OrdinalIgnoreCase)
                && !zone.StartsWith(global + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                roots.Add(zone);
            return roots;
        }
    }
}
