using Microsoft.CodeAnalysis;

namespace PlantUmlClassDiagramGenerator.SourceGenerator.Associations;

public record AssociationNode(string Node)
{
    public static AssociationNode Association = new("-left-");
    public static AssociationNode Aggregation = new("o-left-");
    public static AssociationNode Composition = new("*-left-");
    public static AssociationNode Dependency = new(".left.>");
    public static AssociationNode Inheritance = new("<|-down-");
    public static AssociationNode Realization = new("<|.down.");
    public static AssociationNode Nest = new("+.right.");

    public string Node { get; } = Node;

    public Association Create(ITypeSymbol rootSymbol, ITypeSymbol leafSymbol,
        string label = "", string rootLabel = "", string leafLabel = "")
    {
        return new Association(rootSymbol, leafSymbol, this, label, rootLabel, leafLabel);
    }
}
