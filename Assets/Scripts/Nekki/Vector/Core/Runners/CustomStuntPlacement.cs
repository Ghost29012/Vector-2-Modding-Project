using System.Xml;

namespace Nekki.Vector.Core.Runners
{
    internal static class CustomStuntPlacement
    {
        internal static string CardNameFor(XmlElement manifest)
        {
            if (manifest == null) return string.Empty;
            string cardName = manifest.GetAttribute("CardName");
            return string.IsNullOrEmpty(cardName) ? manifest.GetAttribute("Name") + "_1" : cardName;
        }

        internal static string RunImageFor(XmlElement manifest)
        {
            if (manifest == null) return string.Empty;
            string runImage = manifest.GetAttribute("RunImage");
            return string.IsNullOrEmpty(runImage) ? manifest.GetAttribute("Image") : runImage;
        }

        internal static bool Replace(XmlNode stunt, string trickName, string cardName)
        {
            if (stunt == null || stunt.Attributes == null || stunt.Attributes["Name"] == null
                || stunt.Attributes["Name"].Value != "Stunt" || string.IsNullOrEmpty(trickName)
                || string.IsNullOrEmpty(cardName) || stunt["Content"] == null
                || stunt.OwnerDocument == null)
                return false;

            XmlNode content = stunt["Content"];
            while (content.HasChildNodes) content.RemoveChild(content.FirstChild);

            XmlDocument document = stunt.OwnerDocument;
            XmlElement reference = document.CreateElement("ObjectReference");
            reference.SetAttribute("Name", "StuntReal");
            reference.SetAttribute("Filename", "triggers.xml");
            reference.SetAttribute("X", "0");
            reference.SetAttribute("Y", "0");

            XmlElement properties = document.CreateElement("Properties");
            XmlElement statics = document.CreateElement("Static");
            XmlElement overrides = document.CreateElement("OverrideVariable");
            AddOverride(document, overrides, "StuntName", trickName);
            AddOverride(document, overrides, "CardName", cardName);
            statics.AppendChild(overrides);
            properties.AppendChild(statics);
            reference.AppendChild(properties);
            content.AppendChild(reference);
            return true;
        }

        private static void AddOverride(XmlDocument document, XmlElement parent, string name, string value)
        {
            XmlElement variable = document.CreateElement("Variable");
            variable.SetAttribute("Name", name);
            variable.SetAttribute("Value", value);
            parent.AppendChild(variable);
        }
    }
}
