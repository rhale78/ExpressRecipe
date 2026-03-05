using System.Text;
using System.Xml;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class RecipeMLExporter : IRecipeExporter
{
    public string FormatName => "RecipeML";
    public string DefaultFileExtension => "rml";

    public string Export(ParsedRecipe recipe)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("recipeml");
            writer.WriteAttributeString("version", "0.5");
            WriteRecipe(writer, recipe);
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("recipeml");
            writer.WriteAttributeString("version", "0.5");
            foreach (var r in recipes) WriteRecipe(writer, r);
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteRecipe(XmlWriter w, ParsedRecipe r)
    {
        w.WriteStartElement("recipe");
        w.WriteElementString("title", r.Title);
        if (!string.IsNullOrEmpty(r.Description)) w.WriteElementString("description", r.Description);
        if (!string.IsNullOrEmpty(r.Author)) w.WriteElementString("source", r.Author);
        if (!string.IsNullOrEmpty(r.Yield)) w.WriteElementString("yield", r.Yield);
        if (!string.IsNullOrEmpty(r.PrepTime)) w.WriteElementString("preptime", r.PrepTime);
        if (!string.IsNullOrEmpty(r.CookTime)) w.WriteElementString("cooktime", r.CookTime);
        if (!string.IsNullOrEmpty(r.Category)) w.WriteElementString("categories", r.Category);

        if (r.Ingredients.Count > 0)
        {
            w.WriteStartElement("ingredients");
            foreach (var ing in r.Ingredients)
            {
                w.WriteStartElement("ing");
                if (!string.IsNullOrEmpty(ing.Quantity)) w.WriteElementString("amt", ing.Quantity);
                if (!string.IsNullOrEmpty(ing.Unit)) w.WriteElementString("unit", ing.Unit);
                w.WriteElementString("item", ing.Name);
                if (!string.IsNullOrEmpty(ing.Preparation)) w.WriteElementString("prep", ing.Preparation);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        if (r.Instructions.Count > 0)
        {
            w.WriteStartElement("directions");
            foreach (var inst in r.Instructions)
                w.WriteElementString("step", inst.Text);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }
}
