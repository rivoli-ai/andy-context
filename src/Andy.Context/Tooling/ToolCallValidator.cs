using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Andy.Context.Model;

namespace Andy.Context.Tooling;

/// <summary>
/// Validates the supported JSON Schema subset. Unsupported keywords and malformed
/// schemas fail validation explicitly rather than silently accepting arguments.
/// </summary>
public static class ToolCallValidator
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "type", "properties", "required", "additionalProperties", "items", "enum", "const",
        "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems",
        "title", "description", "default", "examples", "$schema"
    };
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
        { "object", "array", "string", "number", "integer", "boolean", "null" };

    public static ValidationResult Validate(ToolCall call, ToolDeclaration definition)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(definition);
        var result = new ValidationResult();
        try
        {
            if (string.IsNullOrWhiteSpace(call.ArgumentsJson))
                result.Errors.Add("Arguments cannot be empty.");
            else
            {
                using var arguments = JsonDocument.Parse(call.ArgumentsJson);
                var schema = JsonSerializer.SerializeToElement(definition.Parameters);
                CheckSchema(schema, "$schema");
                CheckValue(arguments.RootElement, schema, "$", result.Errors);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException or NotSupportedException)
        {
            result.Errors.Add($"Invalid arguments or schema: {ex.Message}");
        }
        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private static void CheckSchema(JsonElement schema, string path)
    {
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
        if (schema.ValueKind != JsonValueKind.Object) throw new ArgumentException($"{path}: expected schema object or boolean.");
        foreach (var keyword in schema.EnumerateObject())
        {
            if (!Keywords.Contains(keyword.Name))
                throw new NotSupportedException($"{path}: unsupported schema keyword '{keyword.Name}'.");
            var value = keyword.Value;
            switch (keyword.Name)
            {
                case "type":
                    var names = value.ValueKind == JsonValueKind.Array
                        ? value.EnumerateArray().Select(t => t.GetString()).ToArray()
                        : new[] { value.GetString() };
                    if (names.Length == 0 || names.Any(t => t == null || !Types.Contains(t)))
                        throw new ArgumentException($"{path}: invalid type.");
                    break;
                case "properties":
                    foreach (var property in value.EnumerateObject()) CheckSchema(property.Value, path + "." + property.Name);
                    break;
                case "items":
                case "additionalProperties":
                    CheckSchema(value, path + "." + keyword.Name);
                    break;
                case "required":
                    foreach (var required in value.EnumerateArray())
                        if (required.ValueKind != JsonValueKind.String) throw new ArgumentException($"{path}: required must contain strings.");
                    break;
                case "enum":
                    if (value.GetArrayLength() == 0) throw new ArgumentException($"{path}: enum cannot be empty.");
                    break;
                case "minimum":
                case "maximum":
                    _ = ExactDecimal(value);
                    break;
                case "minLength":
                case "maxLength":
                case "minItems":
                case "maxItems":
                    if (value.GetInt32() < 0) throw new ArgumentException($"{path}: limits must be nonnegative integers.");
                    break;
            }
        }
    }

    private static void CheckValue(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        if (schema.ValueKind == JsonValueKind.True) return;
        if (schema.ValueKind == JsonValueKind.False) { errors.Add($"{path}: value is disallowed."); return; }
        if (schema.TryGetProperty("type", out var type))
        {
            var matches = type.ValueKind == JsonValueKind.Array
                ? type.EnumerateArray().Any(t => Matches(value, t.GetString()!))
                : Matches(value, type.GetString()!);
            if (!matches) { errors.Add($"{path}: expected {type.GetRawText()}."); return; }
        }
        if (schema.TryGetProperty("enum", out var choices) && !choices.EnumerateArray().Any(c => Equal(value, c)))
            errors.Add($"{path}: value is not in enum.");
        if (schema.TryGetProperty("const", out var constant) && !Equal(value, constant))
            errors.Add($"{path}: value does not match const.");

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (schema.TryGetProperty("required", out var required))
                    foreach (var name in required.EnumerateArray())
                        if (!value.TryGetProperty(name.GetString()!, out _)) errors.Add($"{path}.{name.GetString()}: required property missing.");
                var hasProperties = schema.TryGetProperty("properties", out var properties);
                foreach (var property in value.EnumerateObject())
                {
                    if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
                        CheckValue(property.Value, propertySchema, path + "." + property.Name, errors);
                    else if (schema.TryGetProperty("additionalProperties", out var extra))
                        CheckValue(property.Value, extra, path + "." + property.Name, errors);
                }
                break;
            case JsonValueKind.Array:
                CheckRange(value.GetArrayLength(), schema, "minItems", "maxItems", path, errors);
                if (schema.TryGetProperty("items", out var items))
                {
                    var index = 0;
                    foreach (var item in value.EnumerateArray()) CheckValue(item, items, $"{path}[{index++}]", errors);
                }
                break;
            case JsonValueKind.String:
                CheckRange(value.GetString()!.EnumerateRunes().Count(), schema, "minLength", "maxLength", path, errors);
                break;
            case JsonValueKind.Number:
                if (schema.TryGetProperty("minimum", out _) || schema.TryGetProperty("maximum", out _))
                    CheckRange(ExactDecimal(value), schema, "minimum", "maximum", path, errors);
                break;
        }
    }

    private static bool Equal(JsonElement left, JsonElement right)
        => JsonNode.DeepEquals(JsonNode.Parse(left.GetRawText()), JsonNode.Parse(right.GetRawText()));

    private static bool Matches(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "number" => value.ValueKind == JsonValueKind.Number,
        "integer" => value.ValueKind == JsonValueKind.Number && NumberParts(value.GetRawText()).Scale <= 0,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => false
    };

    // Normalize decimal notation without rounding tiny fractions into integers.
    private static (bool Negative, string Digits, BigInteger Scale) NumberParts(string raw)
    {
        var negative = raw.StartsWith('-');
        raw = raw.TrimStart('-');
        var pieces = raw.Split(new[] { 'e', 'E' }, 2);
        var exponent = pieces.Length == 2 ? BigInteger.Parse(pieces[1], CultureInfo.InvariantCulture) : BigInteger.Zero;
        var point = pieces[0].IndexOf('.');
        var scale = (point < 0 ? 0 : pieces[0].Length - point - 1) - exponent;
        var digits = pieces[0].Replace(".", "").TrimStart('0');
        if (digits.Length == 0) return (false, "0", BigInteger.Zero);
        var trimmed = digits.TrimEnd('0');
        return (negative, trimmed, scale - (digits.Length - trimmed.Length));
    }

    private static decimal ExactDecimal(JsonElement value)
    {
        var number = value.GetDecimal();
        if (NumberParts(value.GetRawText()) != NumberParts(number.ToString(CultureInfo.InvariantCulture)))
            throw new ArgumentException("Numeric bounds require an exact .NET decimal representation.");
        return number;
    }

    private static void CheckRange(decimal actual, JsonElement schema, string min, string max, string path, List<string> errors)
    {
        if (schema.TryGetProperty(min, out var low) && actual < ExactDecimal(low)) errors.Add($"{path}: below {min}.");
        if (schema.TryGetProperty(max, out var high) && actual > ExactDecimal(high)) errors.Add($"{path}: above {max}.");
    }
}
