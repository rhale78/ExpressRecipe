using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HighSpeedDAL.SourceGenerators;

/// <summary>
/// Syntax receiver that collects all classes with DAL-related attributes during compilation
/// </summary>
internal sealed class DalSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> EntityCandidates { get; } = new List<ClassDeclarationSyntax>();
    public List<ClassDeclarationSyntax> ConnectionCandidates { get; } = new List<ClassDeclarationSyntax>();
    public List<string> AllClassesFound { get; } = new List<string>();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Look for class declarations
        if (syntaxNode is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        // Track all classes for debugging
        AllClassesFound.Add(classDeclaration.Identifier.Text);

        // Check if class has any attributes
        if (classDeclaration.AttributeLists.Count == 0 && classDeclaration.BaseList == null)
        {
            return;
        }

        // Check for DAL entity attributes
        foreach (AttributeListSyntax attributeList in classDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                string attributeName = attribute.Name.ToString();

                // Check for [Table], [DalEntity] or [ReferenceTable]
                if (attributeName == "Table" ||
                    attributeName == "TableAttribute" ||
                    attributeName == "DalEntity" ||
                    attributeName == "DalEntityAttribute" ||
                    attributeName == "ReferenceTable" ||
                    attributeName == "ReferenceTableAttribute")
                {
                    EntityCandidates.Add(classDeclaration);
                    return; // Found a matching attribute, no need to check others for this class
                }
            }
        }

        // Check for DatabaseConnectionBase inheritance
        if (classDeclaration.BaseList != null)
        {
            foreach (BaseTypeSyntax baseType in classDeclaration.BaseList.Types)
            {
                string baseTypeName = baseType.Type.ToString();
                // Check for DatabaseConnectionBase in any form (fully qualified or simple name)
                if (baseTypeName.Contains("DatabaseConnectionBase") ||
                    baseTypeName.EndsWith("ConnectionBase"))
                {
                    ConnectionCandidates.Add(classDeclaration);
                    break;
                }
            }
        }
    }
}
