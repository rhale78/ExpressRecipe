using System.Xml;

namespace ExpressRecipe.RecipeParser.Helpers;

public static class XmlParserHelper
{
    public static XmlDocument LoadXml(string text)
    {
        var doc = new XmlDocument();
        doc.LoadXml(text);
        return doc;
    }

    public static string? GetElementText(XmlNode? parent, string tagName)
    {
        if (parent == null) return null;
        var node = parent.SelectSingleNode(tagName);
        return node?.InnerText?.Trim();
    }

    public static string? GetAttributeValue(XmlNode? node, string attrName)
    {
        if (node?.Attributes == null) return null;
        return node.Attributes[attrName]?.Value;
    }

    public static List<XmlNode> GetChildNodes(XmlNode? parent, string tagName)
    {
        if (parent == null) return new();
        var result = new List<XmlNode>();
        var nodes = parent.SelectNodes(tagName);
        if (nodes != null)
            foreach (XmlNode n in nodes) result.Add(n);
        return result;
    }
}
