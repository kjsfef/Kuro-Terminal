using System;

namespace Kuro;

public sealed record CommandDefinition(
    string Name,
    string Category,
    string Usage,
    string Description,
    Func<string[], string, ShellResult> Handler);
