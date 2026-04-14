# Agent Guidelines

## Spectre.Console
- **Escape Placeholders**: Always wrap dynamic values in `Markup.Escape(val)` when using `AnsiConsole.MarkupLine` to avoid runtime parsing errors.
- **Literal Brackets**: Double literal brackets in format strings (e.g., `[[DRY RUN]]`).
- **Intentional Markup**: Do not escape strings already purposefully constructed as markup (e.g., `"[green]ok[/]"`).
- **JSON Output**: For syntax-highlighted terminal JSON, always use `AnsiConsole.Write(new JsonText(json))`. Never write raw JSON strings.

## Code Standards
- **XMLDocs**: Use block comment format ONLY: `/* <summary>...</summary> */`. Do not use `///`.
- **Dependencies**: Always verify the latest package version before updating `.csproj` files.
- **Magic Strings**: Use the `Constants` class for all literal strings. No hardcoded magic strings.
- **Instantiation**: Require explicit typing with target-typed `new()`: `TargetType obj = new();` (NO `var obj = new TargetType();`).
- **Collections**: Always use the new form `List<string> foo = ['a', 'b', 'c'];` (NO `var foo = new List<string>();`). (NO `var foo = new string[] { 'a', 'b', 'c' };`).
