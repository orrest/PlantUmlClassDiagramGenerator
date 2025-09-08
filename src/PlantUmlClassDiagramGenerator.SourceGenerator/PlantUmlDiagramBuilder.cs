﻿using Microsoft.CodeAnalysis;
using PlantUmlClassDiagramGenerator.SourceGenerator.Associations;
using PlantUmlClassDiagramGenerator.SourceGenerator.Extensions;
using PlantUmlClassDiagramGenerator.SourceGenerator.Options;
using System.Collections.Immutable;
using System.Text;

namespace PlantUmlClassDiagramGenerator.SourceGenerator;

internal class PlantUmlDiagramBuilder(
    INamedTypeSymbol symbol,
    GeneratorOptions options,
    string indent = "    ")
{
    private GeneratorOptions Options { get; } = options;
    private INamedTypeSymbol Symbol { get; } = symbol;
    private IList<string> MemberDeclarations { get; } = new List<string>();
    private ISet<Association> Associations { get; } = new HashSet<Association>();
    private ISet<string> IncludeItems { get; } = new HashSet<string>();
    private IImmutableSet<INamedTypeSymbol> ExtraAssociationTargets { get; } = symbol.GetExtraAssociationTargets();

    public string Indent { get; set; } = indent;
    public string UmlString { get; private set; } = "";


    public string Build(IImmutableSet<INamedTypeSymbol> symbols)
    {
        Clear();
        SetInheritance(symbols);
        SetRealization(symbols);
        ProcessMembersSymbol(symbols);
        UmlString = MakeUmlString();
        return UmlString;
    }

    public void Write()
    {
        var file = Symbol.GetOutputFilePath(Options.OutputDir);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllText(file, UmlString);
    }

    private void Clear()
    {
        MemberDeclarations.Clear();
        Associations.Clear();
        IncludeItems.Clear();
        UmlString = "";
    }

    private void ProcessMembersSymbol(IImmutableSet<INamedTypeSymbol> symbols)
    {
        foreach (var member in Symbol.GetMembers().Where(memberSymbol => GeneratorAttributes.MemberTypeFilter(memberSymbol, Symbol)))
        {
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    if (!fieldSymbol.Name.StartsWith("<")) //skip "<PropName>N_BackingField";
                    {
                        SetFieldDeclaration(fieldSymbol);
                        SetFieldAssociation(fieldSymbol, symbols);
                    }
                    break;
                case IPropertySymbol propertySymbol:
                    // Skip compiler-generated properties.
                    if (!propertySymbol.IsImplicitlyDeclared)
                    {
                        SetPropertyDeclaration(propertySymbol);
                        SetPropertyAssociation(propertySymbol, symbols);
                    }
                    break;
                case IMethodSymbol methodSymbol:
                    if (!methodSymbol.IsSoleRecordConstructor() // Only include constructor when there is more than one.
                        && methodSymbol.MethodKind is not MethodKind.PropertyGet
                        and not MethodKind.PropertySet
                        and not MethodKind.EventAdd
                        and not MethodKind.EventRemove
                        && !methodSymbol.Name.StartsWith("<")) //skip property Getter/Setter & event Add/Remove & <Clone>
                    {
                        SetMethodDeclaration(methodSymbol);
                        SetMethodAssociation(methodSymbol, symbols);
                    }
                    break;
                case IEventSymbol eventSymbol:
                    SetEventDeclaration(eventSymbol);
                    break;
                case INamedTypeSymbol nestedType:
                    SetNest(nestedType, symbols);
                    break;
            }
        }
    }

    private string MakeUmlString()
    {
        var sb = new StringBuilder();
        //@startuml
        sb.AppendLine($"@startuml {Symbol.GetMetadataName()}");

        // !theme
        sb.AppendLine("!theme reddress-darkblue");

        // straight line
        sb.AppendLine("skinparam linetype ortho");

        sb.AppendLine();

        //!include section
        if (IncludeItems.Count > 0)
        {
            sb.AppendLine(string.Join(Environment.NewLine, IncludeItems.Select(s => $"!include {s}")));
        }
        //type declare
        sb.AppendLine($$"""
            {{GetTypeDeclaration()}} {
            {{string.Join(Environment.NewLine, MemberDeclarations.Select(s => Indent + s))}}
            }
            """);
        //associations
        if (Associations.Count > 0)
        {
            sb.AppendLine(string.Join(Environment.NewLine, Associations.Select(a => a.ToString())));
        }
        //@enduml
        sb.Append("@enduml");
        return sb.ToString();
    }

    private string GetTypeDeclaration()
    {
        var typeKind = Symbol.GetTypeKindString();
        var name = Symbol.GetMetadataName() + Symbol.GetTypeParamtersString();
        var modifiers = Symbol.GetModifiersString();
        return $"{typeKind} {name} {modifiers}";
    }

    private void SetPropertyDeclaration(IPropertySymbol propertySymbol)
    {
        var accessibility = propertySymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = propertySymbol.GetModifiersString();
        var typeName = propertySymbol.GetTypeString();
        var accessors = propertySymbol.GetAccessorString();
        MemberDeclarations.Add($"{accessibility}{modifiers}{propertySymbol.Name} : {typeName} {accessors}");
    }

    private void SetEventDeclaration(IEventSymbol eventSymbol)
    {
        var accessibility = eventSymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = eventSymbol.GetModifiersString();
        var typeName = eventSymbol.GetTypeString();
        MemberDeclarations.Add($"{accessibility}{modifiers}<<event>> {eventSymbol.Name} : {typeName}");
    }

    private void SetFieldDeclaration(IFieldSymbol fieldSymbol)
    {
        if (Symbol.TypeKind == TypeKind.Enum)
        {
            MemberDeclarations.Add(fieldSymbol
                .ToDisplayString(new SymbolDisplayFormat(
                    memberOptions: SymbolDisplayMemberOptions.IncludeConstantValue)));
            return;
        }
        var accessibility = fieldSymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = fieldSymbol.GetModifiersString();
        var typeName = fieldSymbol.GetTypeString();
        MemberDeclarations.Add($"{accessibility}{modifiers}{fieldSymbol.Name} : {typeName}");
    }

    private void SetMethodDeclaration(IMethodSymbol methodSymbol)
    {
        var accessibility = methodSymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = methodSymbol.GetModifiersString();
        var returnType = methodSymbol.GetReturnTypeString();
        var parameters = methodSymbol.GetParametersString();
        var name = methodSymbol.GetName();
        MemberDeclarations.Add($"{accessibility}{modifiers}{name}({parameters}){returnType}");
    }

    private void SetPropertyAssociation(IPropertySymbol propertySymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (Symbol.DisableAssociationTypes().HasFlag(AssociationTypes.Property))
        {
            return;
        }
        if (propertySymbol.HasPlantUmlIgnoreAssociationAttribute())
        {
            return;
        }
        if (propertySymbol.HasPlantUmlAssociationAttribute())
        {
            SetCustomAssociation(propertySymbol, propertySymbol.Type, symbols);
            return;
        }
        var targetType = propertySymbol.Type;
        var leafLabel = "";

        var ie = propertySymbol.Type.AllInterfaces
            .FirstOrDefault(x => x.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        if (ie != null)
        {
            targetType = ie.TypeArguments[0];
            leafLabel = "*";
        }
        else if (propertySymbol.Type is IArrayTypeSymbol arrayType)
        {
            targetType = arrayType.ElementType;
            leafLabel = "*";
        }

        if (targetType is INamedTypeSymbol typeSymbol
                && (HasReference(typeSymbol, symbols) || IsExtraAssociationTarget(typeSymbol, ExtraAssociationTargets))
                && !typeSymbol.Equals(Symbol, SymbolEqualityComparer.Default))
        {
            if (leafLabel == "")
            {
                leafLabel = typeSymbol.IsGenericType ? typeSymbol.GetTypeArgumentsString() : "";
            }
            if (propertySymbol.HasPropertyInitializer()
                || Symbol.ContainsObjectCreationInConstructor(propertySymbol.Name))
            {
                Associations.Add(AssociationNode.Composition.Create(
                    Symbol,
                    typeSymbol,
                    label: propertySymbol.Name,
                    leafLabel: leafLabel));
            }
            else
            {
                Associations.Add(AssociationNode.Aggregation.Create(
                    Symbol,
                    typeSymbol,
                    label: propertySymbol.Name,
                    leafLabel: leafLabel));
            }
            AddToIncludeItems(typeSymbol, symbols);
        }
    }

    private void SetCustomAssociation(ISymbol targetSymbol, ITypeSymbol targetTypeSymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        var leafTypeSymbol = targetSymbol.GetPlantUmlAssociationAttributeArg("LeafType") as INamedTypeSymbol
            ?? targetTypeSymbol;
        var node = targetSymbol.GetPlantUmlAssociationAttributeArg(0) as string ?? AssociationNode.Association.Node;
        var lLabel = targetSymbol.GetPlantUmlAssociationAttributeArg("LeafLabel") as string ?? "";
        var nLabel = targetSymbol.GetPlantUmlAssociationAttributeArg("NodeLabel") as string ?? "";
        var rLabel = targetSymbol.GetPlantUmlAssociationAttributeArg("RootLabel") as string ?? "";
        Associations.Add(new Association(
            Symbol,
            leafTypeSymbol,
            new AssociationNode(node),
            nLabel,
            rLabel,
            lLabel));
        if (leafTypeSymbol is INamedTypeSymbol namedTypeSymbol)
        {
            AddToIncludeItems(namedTypeSymbol, symbols);
        }
    }

    private void SetFieldAssociation(IFieldSymbol fieldSymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (Symbol.DisableAssociationTypes().HasFlag(AssociationTypes.Field))
        {
            return;
        }
        if (fieldSymbol.HasPlantUmlIgnoreAssociationAttribute())
        {
            return;
        }
        if (fieldSymbol.HasPlantUmlAssociationAttribute())
        {
            SetCustomAssociation(fieldSymbol, fieldSymbol.Type, symbols);
            return;
        }
        var targetType = fieldSymbol.Type;
        var leafLabel = "";

        var ie = fieldSymbol.Type.AllInterfaces
            .FirstOrDefault(x => x.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        if (ie != null)
        {
            targetType = ie.TypeArguments[0];
            leafLabel = "*";
        }
        else if (fieldSymbol.Type is IArrayTypeSymbol arrayType)
        {
            targetType = arrayType.ElementType;
            leafLabel = "*";
        }

        if (targetType is INamedTypeSymbol typeSymbol
            && (HasReference(typeSymbol, symbols) || IsExtraAssociationTarget(typeSymbol, ExtraAssociationTargets))
            && !typeSymbol.Equals(Symbol, SymbolEqualityComparer.Default))
        {
            if (leafLabel == "")
            {
                leafLabel = typeSymbol.IsGenericType ? typeSymbol.GetTypeArgumentsString() : "";
            }

            if (fieldSymbol.HasFieldInitializer()
                || Symbol.ContainsObjectCreationInConstructor(fieldSymbol.Name))
            {
                Associations.Add(AssociationNode.Composition.Create(
                    Symbol,
                    typeSymbol,
                    label: fieldSymbol.Name,
                    leafLabel: leafLabel));
            }
            else
            {
                Associations.Add(AssociationNode.Aggregation.Create(
                    Symbol,
                    typeSymbol,
                    label: fieldSymbol.Name,
                    leafLabel: leafLabel));
            }

            AddToIncludeItems(typeSymbol, symbols);
        }
    }

    private void SetMethodAssociation(IMethodSymbol methodSymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (Symbol.DisableAssociationTypes().HasFlag(AssociationTypes.MethodParameter))
        {
            return;
        }
        foreach (var parameter in methodSymbol.Parameters)
        {
            if (parameter.HasPlantUmlIgnoreAssociationAttribute())
            {
                continue;
            }
            if (parameter.HasPlantUmlAssociationAttribute())
            {
                SetCustomAssociation(parameter, parameter.Type, symbols);
                continue;
            }

            if (parameter.Type is INamedTypeSymbol typeSymbol
                && (HasReference(typeSymbol, symbols) || IsExtraAssociationTarget(typeSymbol, ExtraAssociationTargets))
                && !typeSymbol.Equals(Symbol, SymbolEqualityComparer.Default))
            {
                var leafLabel = typeSymbol.IsGenericType ? typeSymbol.GetTypeArgumentsString() : "";
                Associations.Add(AssociationNode.Dependency.Create(Symbol, typeSymbol, leafLabel: leafLabel));
                AddToIncludeItems(typeSymbol, symbols);
            }
        }
    }

    private void SetInheritance(IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (Symbol.DisableAssociationTypes().HasFlag(AssociationTypes.Inheritance))
        {
            return;
        }
        if (Symbol.BaseType is not null
            && (HasReference(Symbol.BaseType, symbols)
            || IsExtraAssociationTarget(Symbol.BaseType, ExtraAssociationTargets)))
        {
            var rootLabel = Symbol.BaseType.IsGenericType ? Symbol.BaseType.GetTypeArgumentsString() : "";
            Associations.Add(AssociationNode.Inheritance.Create(Symbol.BaseType, Symbol, rootLabel: rootLabel));
            AddToIncludeItems(Symbol.BaseType, symbols);
        }
    }

    private void SetRealization(IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (Symbol.DisableAssociationTypes().HasFlag(AssociationTypes.Realization))
        {
            return;
        }
        foreach (var type in Symbol.Interfaces)
        {
            if (HasReference(type, symbols)
                || IsExtraAssociationTarget(type, ExtraAssociationTargets))
            {
                var rootLabel = type.IsGenericType ? type.GetTypeArgumentsString() : "";
                Associations.Add(AssociationNode.Realization.Create(type, Symbol, rootLabel: rootLabel));
                AddToIncludeItems(type, symbols);
            }
        }
    }
    private void SetNest(INamedTypeSymbol nestedTypeSymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (Symbol.DisableAssociationTypes().HasFlag(AssociationTypes.Nest))
        {
            return;
        }
        if (nestedTypeSymbol.HasPlantUmlIgnoreAssociationAttribute())
        {
            return;
        }
        Associations.Add(AssociationNode.Nest.Create(Symbol, nestedTypeSymbol));
        AddToIncludeItems(nestedTypeSymbol, symbols);
    }

    private void AddToIncludeItems(INamedTypeSymbol symbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (HasReference(symbol, symbols))
        {
            IncludeItems.Add(MakeIncludePath(symbol));
        }
    }

    private bool HasReference(INamedTypeSymbol symbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        return ContainsType(symbol, symbols) || File.Exists(symbol.GetOutputFilePath(Options.OutputDir));
    }

    private static bool ContainsType(INamedTypeSymbol symbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        var target = symbol.IsGenericType
                ? symbol.OriginalDefinition
                : symbol;
        return symbols.Contains(target);
    }

    private static bool IsExtraAssociationTarget(INamedTypeSymbol symbol, IImmutableSet<INamedTypeSymbol> extraAssociationTargets)
    {
        var target = symbol.IsGenericType
                ? symbol.OriginalDefinition
                : symbol;
        return extraAssociationTargets.Contains(target);
    }

    private string MakeIncludePath(ITypeSymbol targetSymbol)
    {
        string[] self = [Symbol.ContainingAssembly.Name, .. Symbol.ContainingNamespace.ToString().Split('.')];
        string[] target = [targetSymbol.ContainingAssembly.Name, .. targetSymbol.ContainingNamespace.ToString().Split('.')];
        return GetRelativeIncludePath(self, target) + $"/{targetSymbol.GetMetadataName(".")}.puml";
    }

    private static string GetRelativeIncludePath(string[] self, string[] target)
    {
        if (self.SequenceEqual(target))
        {
            return ".";
        }
        int i;
        for (i = 0; i < Math.Min(self.Length, target.Length); i++)
        {
            if (self[i] != target[i]) { break; }
        }
        string[] values = [.. Enumerable.Repeat("..", self.Length - i), .. target.AsSpan(i)];
        return string.Join("/", values);
    }
}




