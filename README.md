# Project Overview

`PlantUmlClassDiagramGenerator` is a tool that automatically generates PlantUML class diagrams from C# source code. The project provides multiple integration options to help developers quickly visualize C# code structure and relationships.

---

# Core Features

The tool analyzes C# source code and generates standard PlantUML class diagrams, supporting:

- **Type declarations**: including class, struct, interface, enum, record types  
- **Type modifiers**: `abstract`, `static`, `partial`, `sealed` modifiers  
- **Member visibility**: `public`, `private`, `protected`, `internal` access modifiers  
- **Relationship mapping**: automatic identification of inheritance, implementation, and association relationships  

---

# Roslyn Source Generator

The most advanced approach: automatically generating class diagrams at compile time using a Source Generator.  
This method uses compiler symbol information rather than syntax trees for more accurate code structure analysis.

## Steps:

1. Install the NuGet package in the project you want to generate class diagrams from:

    ```shell
    PlantUML.ClsDig.Src.Gen
    ```

    *(If your class files are distributed across multiple projects, install the package in each one.)*

2. In your project's `.csproj` file:

    ```xml
    <PropertyGroup>
        <DefineConstants>$(DefineConstants);GENERATE_PLANTUML</DefineConstants>
    </PropertyGroup>
    <ItemGroup>
        <CompilerVisibleProperty Include="PlantUmlGenerator_OutputDir" />
    </ItemGroup>
    <PropertyGroup>
        <PlantUmlGenerator_OutputDir>$(SolutionDir)umls\generated-uml</PlantUmlGenerator_OutputDir>
    </PropertyGroup>
    ```

    - The constant `GENERATE_PLANTUML` triggers the generator.
    - If you have class diagrams across multiple projects, specify the **same output folder** so they can reference each other.

3. Add the attribute in your `AssemblyInformation.cs` or directly on classes:

    ```csharp
    [assembly: PlantUmlClassDiagramGenerator.SourceGenerator.Attributes.PlantUmlDiagram]
    ```

See the original [README](https://github.com/pierre3/PlantUmlClassDiagramGenerator/blob/master/src/PlantUmlClassDiagramGenerator.SourceGenerator/README.md).

---

# What Is This Fork For?

1. Added `reddress-darkblue` as the default theme in the source generator library.  
2. Specified the direction of the associations in the source generator:
    - Implementations and inheritances are **vertical**
    - Compositions, aggregations, and dependencies are **horizontal**

---

# Note

- If the theme is not previewing correctly in VS Code, consider [updating the PlantUML version](https://github.com/qjebbs/vscode-plantuml/issues/407#issuecomment-925692712).
- For the usage of VS extension and the dotnet CLI tool, see the original project.
