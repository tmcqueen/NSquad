# NSquad — Coding Guidelines

## Spectre.Console: Markup Safety

### Always escape string placeholders

`AnsiConsole.MarkupLine` parses `[tag]` sequences in both the format string **and** the values passed to `{0}`, `{1}`, etc. Any string from user input, file data, process output, or exception messages that contains `[` will cause a runtime error like `Error: Could not find color or style 'X'`.

**Rule:** wrap every string placeholder value in `Markup.Escape()`.

```csharp
// WRONG — ex.Message or settings.Name could contain '['
AnsiConsole.MarkupLine("[red]✗[/] {0}", ex.Message);
AnsiConsole.MarkupLine("[green]✓[/] Added: [bold]{0}[/]", settings.Name);

// CORRECT
AnsiConsole.MarkupLine("[red]✗[/] {0}", Markup.Escape(ex.Message));
AnsiConsole.MarkupLine("[green]✓[/] Added: [bold]{0}[/]", Markup.Escape(settings.Name));
```

This applies to: exception messages, user-supplied arguments, file paths, names read from JSON files, strings from external process output (e.g. `gh` CLI), and any other runtime string.

### Escaping literal brackets in format strings

If you need to print a literal `[` in the format string itself, double the bracket:

```csharp
// WRONG — Spectre tries to parse [DRY RUN] as a tag
AnsiConsole.MarkupLine("[dim][DRY RUN][/] Would write files.");

// CORRECT
AnsiConsole.MarkupLine("[dim][[DRY RUN]][/] Would write files.");
```

### Exception: intentional inline markup

Do **not** escape strings that are intentionally constructed as markup. These are fine:

```csharp
var status = enabled ? "[green]enabled[/]" : "[dim]disabled[/]";
AnsiConsole.MarkupLine("  Status: {0}", status); // intentional — do not escape
```

---

## Spectre.Console: JSON Terminal Output

When printing JSON to the terminal, use `Spectre.Console.Json` for syntax-highlighted output. Do not use `AnsiConsole.Write(jsonString)` or `Console.WriteLine(jsonString)`.

```csharp
using Spectre.Console.Json;

// WRONG
AnsiConsole.Write(json);
Console.WriteLine(json);

// CORRECT
AnsiConsole.Write(new JsonText(json));
```

`Spectre.Console.Json` is already a project dependency (`Spectre.Console.Json 0.55.0`). No additional package reference is needed.
