using System.Text.Json;
using Andy.Context.Model;

namespace Andy.Context.Tooling;

/// <summary>
/// Tool call validator against JSON Schema.
/// </summary>
public static class ToolCallValidator
{
    public static ValidationResult Validate(ToolCall call, ToolDeclaration definition)
    {
        var result = new ValidationResult { IsValid = true };
        
        try
        {
            // Basic validation - check if arguments can be parsed
            var args = call.ArgumentsAsJsonElement();
            
            // TODO: Add full JSON Schema validation when needed
            // For now, just ensure it's valid JSON
            if (string.IsNullOrWhiteSpace(call.ArgumentsJson))
            {
                result.Errors.Add("Arguments cannot be empty");
                result.IsValid = false;
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON arguments: {ex.Message}");
            result.IsValid = false;
        }
        
        return result;
    }
}