# NSquad — Agent Guidelines

## Spectre.Console: Markup Safety

### Always escape string placeholders

`AnsiConsole.MarkupLine` parses `[tag]` sequences in both the format string **and** the values passed to `{0}`, `{1}`, etc. Any unescaped string containing `[` will cause a runtime error like `Error: Could not find color or style 'X'`.

**Rule:** wrap every string placeholder value in `Markup.Escape()`.

```csharp
// WRONG
AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
AnsiConsole.MarkupLine("[green]✓[/] Added: [bold]{0}[/]", settings.Name);

// CORRECT
AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message));
AnsiConsole.MarkupLine("[green]✓[/] Added: [bold]{0}[/]", Markup.Escape(settings.Name));
```

Applies to: exception messages, user-supplied arguments, file paths, names read from JSON files, process output, and any other runtime string.

### Escaping literal brackets in format strings

Double any `[` that should appear as literal text in the format string:

```csharp
// WRONG — Spectre parses [DRY RUN] as a tag
AnsiConsole.MarkupLine("[dim][DRY RUN][/] Would write files.");

// CORRECT
AnsiConsole.MarkupLine("[dim][[DRY RUN]][/] Would write files.");
```

### Exception: intentional inline markup

Do **not** escape strings that are intentionally constructed as markup:

```csharp
var status = enabled ? "[green]enabled[/]" : "[dim]disabled[/]";
AnsiConsole.MarkupLine("  Status: {0}", status); // intentional — do not escape
```

---

## Spectre.Console: JSON Terminal Output

Use `Spectre.Console.Json` for syntax-highlighted JSON output. Do not write raw JSON strings to the console.

```csharp
using Spectre.Console.Json;

// WRONG
AnsiConsole.Write(json);
Console.WriteLine(json);

// CORRECT
AnsiConsole.Write(new JsonText(json));
```

`Spectre.Console.Json 0.55.0` is already referenced in `Squad.Cli.csproj`.
