# CLAUDE.md - AI Assistant Guide for ExpressRecipe

**Last Updated:** 2025-11-14
**Project Status:** Early-stage development (partially implemented, untested)

---

## Project Overview

**ExpressRecipe** is a template-driven code generation engine written in C# that automates boilerplate code creation from database schemas. The name reflects providing "express" (quick) code generation using template "recipes."

### Core Capabilities
- Database schema introspection (SQL Server)
- Template-based code generation with dynamic expressions
- Multiple output strategies (file, console, string, debug)
- Extensible parser and command system
- Support for C# and SQL code file generation
- Configurable file overwrite behavior

### Technology Stack
- **Language:** C# (.NET Framework 4.5.2)
- **UI:** Windows Forms
- **Database:** SQL Server (via ADO.NET)
- **Testing:** Microsoft Pex Framework
- **Platform:** AnyCPU

---

## Codebase Structure

```
ExpressRecipe/
├── CodeGenerator/                    # Main module (all core logic)
│   ├── CodeGenerator/               # Core library
│   │   ├── Templates/               # Template processing engine
│   │   │   ├── Parser/              # Template syntax parsing
│   │   │   ├── Interpreter/         # Command execution engine
│   │   │   ├── Commands/            # Command implementations
│   │   │   │   └── Parameters/      # Command parameter types
│   │   │   └── TemplateProcessing/  # Line/part processing
│   │   ├── OutputStrategy/          # Output destination strategies
│   │   ├── SourceDataStrategies/    # Data source integrations
│   │   ├── OverwriteStrategies/     # File overwrite policies
│   │   └── CodeFiles/               # Generated file abstractions
│   ├── CodeGenerator.Winforms/      # Windows Forms UI
│   ├── CodeGenerator.Core.Tests/    # Unit tests (Pex)
│   └── TestApp/                     # Manual test application
├── Logging/                         # Logging infrastructure (stubs)
│   ├── Logging.Core/
│   ├── Logging.BL/
│   ├── Logging.DAL/
│   └── Logging.Database/
└── Main/                           # Main database schemas
    └── Main.Database/
```

### Module Responsibilities

| Module | Purpose | Status |
|--------|---------|--------|
| `CodeGenerator/CodeGenerator` | Core engine, all generation logic | In development |
| `Templates/Parser` | Parse template syntax into commands | Functional |
| `Templates/Interpreter` | Execute commands, manage variables | Functional |
| `Templates/Commands` | Command implementations | Partial |
| `SourceDataStrategies` | Database schema extraction | Functional |
| `OutputStrategy` | Output destination handling | Functional |
| `CodeFiles` | File type abstractions | Partial |
| `Logging.*` | Logging infrastructure | Stub only |
| `CodeGenerator.Winforms` | Visual template editor | Minimal |

---

## Architecture & Design Patterns

### 1. Strategy Pattern (Extensively Used)

**OutputStrategy** - How to write generated code:
```csharp
OutputStrategyBase
├── FileOutputStrategy       // Write to filesystem
├── ConsoleOutputStrategy    // Write to console
├── StringOutputStrategy     // Accumulate in memory
└── DebugOutputStrategy      // Debug output
```

**OverwriteStrategy** - File conflict resolution:
```csharp
OverwriteStrategyBase
├── AlwaysOverwriteStrategy      // Overwrite existing files
├── CreateIfNotExistsStrategy    // Create only if missing
└── FileDefaultStrategy          // Default behavior
```

**DataSourceStrategy** - Where to get schema data:
```csharp
DataSourceStrategyBase
└── DatabaseDataSourceStrategy   // SQL Server introspection
```

### 2. Factory Pattern

All strategies use factory registration:
```csharp
OutputStrategyFactory.GetOutputStrategy("File")
OverwriteStrategyFactory.GetOverwriteStrategy("Always Overwrite")
CodeFileFactory.GetCodeFile(type)
CommandFactory.LoadAllCommands()  // TODO: Not implemented
```

### 3. Parser Pattern (Pratt-like)

Recursive descent parsing with self-describing commands:
```csharp
interface IParsable {
    CommandParserBase GetParser();
}

CommandParserBase
├── PrefixParser         // "bool myVar"
├── InfixParser          // "x = 5"
├── SurroundParser       // "!@ content @!"
├── BlockParser
├── RegexParser
└── LineParserBase
    ├── BoolParser
    ├── IntegerParser
    ├── DoubleParser
    └── FallbackParser
```

### 4. Command Pattern

Commands represent template operations:
```csharp
CommandBase
├── VariableDeclarationCommand<T>
│   ├── BoolVariableDeclarationCommand
│   ├── IntegerVariableDeclarationCommand
│   └── StringVariableDeclarationCommand
├── AssignmentCommand
├── VariableCommand
├── GlobalVariableCommand
└── NullCommand
```

### 5. Template Method Pattern

Abstract base classes define execution flow; subclasses provide specifics.

---

## Template System Deep Dive

### Template Types

**Static Templates:**
- Pure literal text (no dynamic expressions)
- Loaded as `StaticTemplateLine` instances
- `Generate()` adds all lines to output verbatim
- Use for: Headers, licenses, fixed structures

**Dynamic Templates:**
- Contains expressions marked with `!@` ... `@!` delimiters
- Parsed into commands and executed
- Supports variables, assignments, expressions
- Use for: Loops over tables, conditional logic, data-driven generation

### Delimiter Syntax

**Dynamic Region Markers:**
```
!@ <dynamic expression> @!
```

**Examples:**
```
Static line: This is literal text
Dynamic line: The table name is !@ TableName @!
Mixed line: public class !@ TableName @!Repository { }
```

### Code Generation Pipeline

```
Template File
    ↓
[Load & Classify Lines]
    ↓
Static: StaticTemplateLine → Direct output
Dynamic: DynamicTemplateLine → Parser
    ↓
[TemplateParser]
    ↓
Extract commands from !@ @! regions
    ↓
[CommandInterpreter]
    ↓
Execute commands with data context
    ↓
[CodeFile]
    ↓
[OutputStrategy]
    ↓
Generated File
```

### Variable System

**Variable Types:**
```csharp
VariableBase<T>
├── IntVariable
├── BooleanVariable
└── StringVariable
```

**Scoping:**
- Stack-based with `VariableStack` and `VariableStackFrame`
- Global variables (e.g., `TableName`) persist across scopes
- Local variables destroyed when frame popped

**Global Variables (Set by Interpreter):**
- `TableName` - Current table being processed
- Additional globals can be injected per iteration

### Command Language Features

**Supported Syntax:**
```csharp
// Variable declarations
bool isActive
int count
string name

// Variable assignment
isActive = true
count = TableName.Length
name = "Repository"

// Variable access
!@ name @!
```

**Parameter Types:**
- Literal parameters: `BoolLiteralCommandParameter`, `IntLiteralCommandParameter`, etc.
- Variable references: `VariableNameCommandParameter`

---

## Database Schema Extraction

### DatabaseDataSourceStrategy

**Configuration:**
```csharp
var strategy = new DatabaseDataSourceStrategy();
strategy.Settings = "Data Source=server;Initial Catalog=db;...";
strategy.TableNamesToIgnore = new List<string> { "sysdiagrams" };
```

**Key Methods:**

| Method | Returns | Data Source |
|--------|---------|-------------|
| `GetTableNames()` | `List<string>` | `INFORMATION_SCHEMA.TABLES` |
| `GetColumnsForTable(table)` | `List<ColumnDefinition>` | `INFORMATION_SCHEMA.COLUMNS` |
| `GetIndexes(tableName)` | `Dictionary<string, List<string>>` | `sys.indexes`, `sys.index_columns` |
| `GetReferencedTablesForColumn()` | Foreign key data | `sys.foreign_key_columns` |
| `GetUniqueKeys()` | Unique constraints | `sys.key_constraints` |
| `GetAllTables()` | `List<TableDefinition>` | Combines all above |

**Data Objects:**
```csharp
TableDefinition {
    string TableName
    List<ColumnDefinition> Columns
    Dictionary<string, List<string>> Indexes
}

ColumnDefinition {
    string ColumnName
    int OrdinalPosition
    bool IsNullable
    string DataType
    int? MaxCharLength
    bool IsIndexColumn
    bool IsUniqueKeyColumn
    List<ReferencedTable> ReferencedTables
}

ReferencedTable {
    string TableName
    string ColumnName
}
```

---

## Code File System

### File Type Hierarchy

```csharp
CodeFileBase
├── TextualFileBase          // Simple text files
├── IndentableCodeFile       // Supports auto-indentation
│   ├── CSharpCodeFile       // C# with indentation rules
│   └── SQLCodeFile          // SQL with indentation rules
└── StructuredFileBase       // Extends IndentableCodeFile
```

### Key Properties

```csharp
class CodeFileBase {
    string FileName { get; set; }
    string FilePath { get; set; }        // Relative to RootPath
    string FileExtension { get; set; }
    string FullFilename { get; }         // RootPath + FilePath + FileName + Extension
    static string RootPath { get; set; } // Global base directory
    OutputStrategyBase OutputStrategy { get; set; }
    OverwriteStrategyBase OverwriteStrategy { get; set; }
}
```

### Indentation Rules

**IndentableCodeFile** supports pattern-based indentation:
```csharp
indentRules.Add(new StringStartsWithIncreaseIndentationRule("{"));
indentRules.Add(new StringStartsWithDecreaseIndentationRule("}"));
```

Automatically adjusts indentation when adding lines with `{` or `}`.

---

## Development Workflows

### Adding a New Command

1. **Create Command Class:**
   ```csharp
   public class MyCommand : CommandBase<MyCommand>
   {
       public override CommandParserBase GetParser() { ... }
       protected override ICommand Create(params object[] parameters) { ... }
       public void Execute(CommandInterpreter interpreter) { ... }
   }
   ```

2. **Implement Parser:**
   ```csharp
   public class MyCommandParser : PrefixParser
   {
       public MyCommandParser() : base("mycommand ") { }
       // Implement parsing logic
   }
   ```

3. **Register in Factory:**
   Add to `CommandFactory.LoadAllCommands()` (currently not implemented)

### Adding a New Output Strategy

1. **Extend OutputStrategyBase:**
   ```csharp
   public class CustomOutputStrategy : OutputStrategyBase
   {
       public override void Write(CodeFileBase codeFile) { ... }
       public override bool Exists(CodeFileBase codeFile) { ... }
       public override string Contents(CodeFileBase codeFile) { ... }
   }
   ```

2. **Register in Factory:**
   ```csharp
   OutputStrategyFactory.Register("Custom", () => new CustomOutputStrategy());
   ```

### Adding a New Data Source

1. **Extend DataSourceStrategyBase:**
   ```csharp
   public class ApiDataSourceStrategy : DataSourceStrategyBase
   {
       public override List<TableDefinition> GetAllTables() { ... }
   }
   ```

2. **Implement Schema Extraction:**
   - Convert API schema to `TableDefinition` objects
   - Populate columns, indexes, relationships

### Adding a New Code File Type

1. **Extend CodeFileBase:**
   ```csharp
   public class TypeScriptCodeFile : IndentableCodeFile
   {
       public TypeScriptCodeFile()
       {
           FileExtension = ".ts";
           // Add indentation rules
       }
   }
   ```

2. **Register in Factory:**
   ```csharp
   CodeFileFactory.Register("TypeScript", () => new TypeScriptCodeFile());
   ```

---

## Git & Commit Conventions

### Recent Development History

```
9e4c3da  MainDB changes
b2ffaf7  Template parsing changes - !@ @! delimiters for templated regions
9c8d637  Code cleanup - classes in separate files, variable commands added
f2522cf  Initial checkin - untested/not complete
b0919fe  Initial commit
```

### Commit Style Observations

- **Format:** Short subject line, optional body with details
- **Tone:** Direct, technical descriptions
- **Status Notes:** Includes "untested/not complete" when applicable
- **Focus:** Feature/change description

### Recommended Commit Message Format

```
<Type>: <Short description>

<Optional detailed explanation>
<Status notes if incomplete/untested>
```

**Types:**
- Feature: New functionality
- Fix: Bug fix
- Refactor: Code restructuring
- Test: Test additions/changes
- Docs: Documentation updates
- Cleanup: Code organization improvements

---

## Testing Approach

### Current Test Coverage

**Project:** `CodeGenerator.Core.Tests`
- **Framework:** Microsoft Pex (parameterized testing)
- **Status:** Mostly stubs with TODO comments
- **Coverage:** Limited to `DatabaseDataSourceStrategy`

**Test Classes:**
```csharp
DatabaseDataSourceStrategyTest
├── GetAllTablesTest()
├── GetColumnsForTableTest()
├── GetIndexesTest()
├── GetReferencedTablesForColumnTest()
├── GetTableNamesTest()
└── GetUniqueKeysTest()
```

### Test Factory Pattern

```csharp
[PexFactoryMethod(typeof(DatabaseDataSourceStrategy))]
public static DatabaseDataSourceStrategy Create(string settings)
{
    return DatabaseDataSourceStrategyFactory.CreateDatabaseDataSourceStrategy(settings);
}
```

### Adding New Tests

1. **Create Test Method:**
   ```csharp
   [PexMethod]
   [PexAllowedException(typeof(ArgumentException))]
   public void MyMethodTest([PexAssumeUnderTest]MyClass target, int param)
   {
       target.MyMethod(param);
       // Assertions
   }
   ```

2. **Configure Pex Attributes:**
   ```csharp
   [PexClass(typeof(MyClass))]
   [PexMaxBranches(2000)]
   [PexMaxConditions(2000)]
   public partial class MyClassTest { }
   ```

3. **Run Pex:**
   - Pex auto-generates test inputs
   - Explores code paths
   - Creates parameterized unit tests

---

## Extension Points & Patterns

### 1. Strategy Extension Points

**Where:** Any `*StrategyBase` class
**How:** Extend base, implement abstract methods, register in factory
**Examples:**
- `DatabaseDataSourceStrategy` → `ApiDataSourceStrategy`
- `FileOutputStrategy` → `CloudStorageOutputStrategy`
- `AlwaysOverwriteStrategy` → `PromptUserStrategy`

### 2. Command Extension Points

**Where:** `CommandBase<T>`
**How:**
1. Implement `GetParser()` to define syntax
2. Implement `Create()` for instantiation
3. Implement `Execute()` for behavior
4. Register in `CommandFactory`

**Example Use Cases:**
- Loop commands (foreach table, foreach column)
- Conditional commands (if/else)
- String manipulation commands
- File system commands

### 3. Parser Extension Points

**Where:** `CommandParserBase`
**How:**
1. Extend appropriate parser type (`PrefixParser`, `InfixParser`, etc.)
2. Implement `CanParse()` and `Parse()`
3. Return command instance with extracted parameters

**Example Use Cases:**
- New operators
- Complex expressions
- Nested structures
- Multi-token patterns

### 4. Code File Extension Points

**Where:** `CodeFileBase`, `IndentableCodeFile`
**How:**
1. Extend base class
2. Set `FileExtension`
3. Add indentation rules if needed
4. Override `Generate()` if custom logic required

**Example Use Cases:**
- JavaScript/TypeScript files
- Python files
- Configuration files (JSON, XML, YAML)
- Documentation files (Markdown)

---

## Common Tasks & Patterns

### Task: Generate Code for All Tables

```csharp
var dataSource = new DatabaseDataSourceStrategy();
dataSource.Settings = connectionString;

var tables = dataSource.GetAllTables();
foreach (var table in tables)
{
    var template = new DynamicTemplate();
    // Load template file

    var interpreter = new CommandInterpreter();
    interpreter.SetGlobalVariable("TableName", table.TableName);

    var codeFile = new CSharpCodeFile();
    codeFile.FileName = table.TableName + "Repository";
    codeFile.OutputStrategy = new FileOutputStrategy();

    template.Generate(codeFile, interpreter);
    codeFile.Write();
}
```

### Task: Parse Template with Dynamic Content

```csharp
var template = new DynamicTemplate();
var lines = File.ReadAllLines("template.txt");

foreach (var line in lines)
{
    if (line.Contains("!@") && line.Contains("@!"))
    {
        template.AddLine(new DynamicTemplateLine(line));
    }
    else
    {
        template.AddLine(new StaticTemplateLine(line));
    }
}
```

### Task: Add Variable to Interpreter

```csharp
var interpreter = new CommandInterpreter();

// Global variable (persists)
interpreter.SetGlobalVariable("TableName", "Users");

// Local variable (current scope)
var stack = interpreter.VariableStack;
stack.PushFrame();
stack.SetVariable(new IntVariable { Name = "count", Value = 5 });
// ... use variable
stack.PopFrame(); // Variable destroyed
```

### Task: Configure Output Behavior

```csharp
var codeFile = new CSharpCodeFile();
codeFile.FileName = "Repository";
codeFile.FilePath = "Generated/Repositories/";
CodeFileBase.RootPath = @"C:\Projects\Output\";
// Final path: C:\Projects\Output\Generated\Repositories\Repository.cs

codeFile.OutputStrategy = OutputStrategyFactory.GetOutputStrategy("File");
codeFile.OverwriteStrategy = OverwriteStrategyFactory.GetOverwriteStrategy("Always Overwrite");

codeFile.Write();
```

---

## Key Conventions & Standards

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Classes | PascalCase | `DatabaseDataSourceStrategy` |
| Interfaces | IPascalCase | `IParsable`, `IConfigDriven` |
| Methods | PascalCase | `GetAllTables()` |
| Properties | PascalCase | `TableName`, `IsNullable` |
| Fields (private) | camelCase | `_connectionString` |
| Parameters | camelCase | `tableName`, `columnDef` |
| Constants | PascalCase | `MaxRetries` |

### File Organization

- **One class per file** (recent cleanup in commit 9c8d637)
- **Namespace matches folder structure:** `CodeGenerator.Templates.Parser`
- **File name matches class name:** `BoolParser.cs` contains `BoolParser`

### Code Patterns

**Factory Pattern Registration:**
```csharp
public static void Initialize()
{
    Register("TypeName", () => new ConcreteType());
}
```

**Strategy Pattern Usage:**
```csharp
public abstract class StrategyBase
{
    public abstract void Execute();
}

public class ConcreteStrategy : StrategyBase
{
    public override void Execute() { ... }
}
```

**Parser Pattern:**
```csharp
public class MyParser : PrefixParser
{
    public MyParser() : base("prefix ") { }

    public override bool CanParse(string[] tokens) { ... }
    public override ICommand Parse(string[] tokens) { ... }
}
```

---

## Important Gotchas & Warnings

### 1. Incomplete Implementation

**Many features are stubs or incomplete:**
- `CommandFactory.LoadAllCommands()` is empty
- Many `Generate()` methods throw `NotImplementedException`
- Logging module contains only placeholder `Class1.cs` files
- WinForms UI is minimal

**Action:** Check implementation status before assuming functionality.

### 2. Template Delimiter Parsing

**Recent change (commit b2ffaf7):** Dynamic regions use `!@` and `@!`.

**Previous delimiter system may differ** - check git history if working with old templates.

**Mixing static and dynamic:** Ensure proper delimiter placement; errors may fail silently.

### 3. Variable Scoping

**Global variables persist forever** - no cleanup mechanism.

**Stack frames must be manually managed:**
```csharp
stack.PushFrame();
// Use variables
stack.PopFrame(); // Don't forget!
```

**Missing PopFrame()** causes memory leaks and variable pollution.

### 4. Factory Registration

**Factories require explicit registration** - types not auto-discovered.

**Adding new strategies requires factory updates:**
```csharp
// In factory class
static Constructor() {
    Register("NewType", () => new NewTypeClass());
}
```

### 5. RootPath Configuration

**CodeFileBase.RootPath is static** - affects ALL code files globally.

**Change carefully:**
```csharp
CodeFileBase.RootPath = @"C:\NewPath\"; // Affects all subsequent file operations
```

**Not thread-safe** - concurrent generation to different paths will conflict.

### 6. Database Connection Management

**No connection pooling or disposal pattern** in `DatabaseDataSourceStrategy`.

**Connections opened/closed per query** - can be inefficient.

**Recommendation:** Refactor to use `using` statements or connection pooling.

### 7. Error Handling

**Limited exception handling** - most methods throw on error.

**Template parsing errors may be cryptic** - no detailed error messages.

**Action:** Add comprehensive try-catch and logging when extending.

### 8. Testing Status

**Tests are stubs** - don't rely on test coverage for correctness.

**No integration tests** - end-to-end workflows untested.

**Action:** Manually test changes thoroughly; add real assertions to tests.

---

## Working with AI Assistants

### Best Practices for AI-Assisted Development

1. **Always check implementation status** before assuming a feature exists
2. **Read recent commit messages** to understand latest changes
3. **Follow existing patterns** (Strategy, Factory, Parser)
4. **Maintain one class per file** convention
5. **Use factories for instantiation**, not direct `new` calls
6. **Test manually** since automated tests are incomplete
7. **Document incomplete features** in commit messages
8. **Preserve architectural patterns** when extending

### Questions to Ask Before Making Changes

- Is this component fully implemented or a stub?
- Which factory needs updating for this new type?
- Does this affect global state (RootPath, factories)?
- Are variable scopes properly managed?
- Is the template delimiter syntax `!@` ... `@!` correctly used?
- Have I followed the naming conventions?
- Does this fit the existing architectural patterns?

### Code Review Checklist

- [ ] One class per file
- [ ] Namespace matches folder structure
- [ ] Factory registration added if new strategy/type
- [ ] Variable stack properly managed (push/pop frames)
- [ ] Exception handling appropriate
- [ ] Template delimiters correct (`!@` ... `@!`)
- [ ] PascalCase for public members, camelCase for private
- [ ] Abstract base used if multiple implementations expected
- [ ] Interface segregation (IParsable, IConfigDriven, etc.)
- [ ] No hardcoded paths or connection strings

### Recommended Development Flow

1. **Explore codebase** - understand current implementation state
2. **Identify pattern** - which architectural pattern applies?
3. **Find similar implementation** - use as template
4. **Extend appropriately** - follow base class contracts
5. **Register in factory** - if applicable
6. **Test manually** - create small test case in TestApp
7. **Document status** - note if incomplete/untested
8. **Commit with clear message** - describe what was added/changed

---

## Quick Reference

### File Locations

| Component | Path |
|-----------|------|
| Template Parser | `CodeGenerator/CodeGenerator/Templates/Parser/` |
| Command Implementations | `CodeGenerator/CodeGenerator/Templates/Commands/` |
| Interpreter | `CodeGenerator/CodeGenerator/Templates/Interpreter/` |
| Data Source Strategies | `CodeGenerator/CodeGenerator/SourceDataStrategies/` |
| Output Strategies | `CodeGenerator/CodeGenerator/OutputStrategy/` |
| Code File Types | `CodeGenerator/CodeGenerator/CodeFiles/` |
| Tests | `CodeGenerator/CodeGenerator.Core.Tests/` |

### Key Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `TemplateParser` | `Templates/Parser/` | Parse template syntax |
| `CommandInterpreter` | `Templates/Interpreter/` | Execute commands |
| `DatabaseDataSourceStrategy` | `SourceDataStrategies/` | Extract DB schema |
| `OutputStrategyFactory` | `OutputStrategy/` | Create output strategies |
| `CommandFactory` | `Templates/Commands/` | Create commands (stub) |
| `VariableStack` | `Templates/Interpreter/` | Manage variable scopes |

### Common Operations

```csharp
// Load template
var template = new DynamicTemplate();
// Parse template file...

// Configure data source
var dataSource = new DatabaseDataSourceStrategy();
dataSource.Settings = "connection_string";

// Create interpreter
var interpreter = new CommandInterpreter();
interpreter.SetGlobalVariable("TableName", "Users");

// Create code file
var codeFile = new CSharpCodeFile();
codeFile.FileName = "UserRepository";
CodeFileBase.RootPath = @"C:\Output\";

// Generate
template.Generate(codeFile, interpreter);
codeFile.Write();
```

---

## Resources

### Project Files
- `.csproj` files contain dependency information
- `Properties/AssemblyInfo.cs` contains version metadata
- No external configuration files (all hardcoded)

### External Dependencies
- .NET Framework 4.5.2 SDK
- SQL Server (for DatabaseDataSourceStrategy)
- Visual Studio (for Pex testing framework)

### Documentation Gaps
- No API documentation or XML comments
- No user guide or template authoring guide
- No architecture decision records (ADRs)
- Limited inline comments

### Future Documentation Needs
1. Template authoring guide (syntax, commands, examples)
2. API documentation (XML comments)
3. Architecture decision records
4. Integration guide (how to use as library)
5. Extension guide (adding commands, strategies, parsers)

---

## Contact & Support

**Project:** ExpressRecipe
**Repository:** /home/user/ExpressRecipe
**Current Branch:** claude/claude-md-mhz33kx5hof78p4b-01YNh5TGrEBZmnhXorytTych
**Git Status:** Clean (at time of CLAUDE.md creation)

For AI assistants: Refer to this guide when making changes. Follow established patterns, maintain consistency, and document your work clearly.

---

*This document is maintained by AI assistants working on the ExpressRecipe codebase. Update it whenever significant architectural changes are made or new patterns are established.*
