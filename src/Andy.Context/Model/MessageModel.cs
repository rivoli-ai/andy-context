// SPDX-License-Identifier: MIT
// LLM-Agnostic Conversation Framework in C# (No external dependencies)
// Target: .NET 10+ (uses only BCL)

namespace Andy.Context.Model;

#region Core Message Model

/// <summary>
/// Roles roughly compatible with common chat schemas.
/// </summary>
public enum Role
{
    System,
    User,
    Assistant,
    Tool
}

#endregion
