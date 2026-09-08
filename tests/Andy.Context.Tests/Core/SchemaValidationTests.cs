using System.Text.Json;
using Andy.Context.Model;
using Andy.Context.Tooling;

namespace Andy.Tests.Context.Core;

public class SchemaValidationTests
{
    [Theory]
    [InlineData("{}", false)]
    [InlineData("{\"expression\":12}", false)]
    [InlineData("{\"expression\":\"ok\"}", true)]
    [InlineData("{\"expression\":\"ok\",\"extra\":1}", false)]
    public void RequiredTypesAndExtraProperties(string arguments, bool valid)
        => Assert.Equal(valid, Validate(arguments, """
            {"type":"object","required":["expression"],"properties":{"expression":{"type":"string"}},"additionalProperties":false}
            """).IsValid);

    [Theory]
    [InlineData("{\"items\":[1,2]}", true)]
    [InlineData("{\"items\":[1,\"two\"]}", false)]
    [InlineData("{\"items\":[]}", false)]
    [InlineData("{\"items\":[1,2,3]}", false)]
    [InlineData("{\"items\":[0]}", false)]
    [InlineData("{\"items\":[10]}", false)]
    public void NestedArrayAndNumberConstraints(string arguments, bool valid)
        => Assert.Equal(valid, Validate(arguments, """
            {"type":"object","properties":{"items":{"type":"array","minItems":1,"maxItems":2,"items":{"type":"integer","minimum":1,"maximum":5}}}}
            """).IsValid);

    [Theory]
    [InlineData("\"a\"", false)]
    [InlineData("\"ab\"", true)]
    [InlineData("\"abcd\"", false)]
    [InlineData("null", true)]
    public void NullableStringLengths(string arguments, bool valid)
        => Assert.Equal(valid, Validate(arguments, """{"type":["string","null"],"minLength":2,"maxLength":3}""").IsValid);

    [Theory]
    [InlineData("""{"enum":["one","two"]}""", "\"one\"", true)]
    [InlineData("""{"enum":["one","two"]}""", "\"three\"", false)]
    [InlineData("""{"const":true}""", "false", false)]
    [InlineData("""{"type":"boolean"}""", "true", true)]
    [InlineData("""{"type":"number"}""", "1.5", true)]
    [InlineData("""{"type":"integer"}""", "1.5", false)]
    [InlineData("""{"additionalProperties":{"type":"string"}}""", "{\"x\":2}", false)]
    public void EnumConstTypesAndAdditionalSchema(string schema, string arguments, bool valid)
        => Assert.Equal(valid, Validate(arguments, schema).IsValid);

    [Theory]
    [InlineData("""{"anyOf":[]}""")]
    [InlineData("""{"properties":{"unused":{"$ref":"remote"}}}""")]
    [InlineData("""{"type":"made-up"}""")]
    [InlineData("""{"required":"x"}""")]
    [InlineData("""{"minItems":-1}""")]
    [InlineData("""{"items":[]}""")]
    public void UnsupportedOrMalformedSchemasFailExplicitly(string schema)
    {
        var result = Validate("{}", schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("schema", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    public void InvalidJsonFails(string arguments)
        => Assert.False(Validate(arguments, "{}").IsValid);

    [Fact]
    public void ToolArguments_ClonedElementRemainsUsable()
        => Assert.Equal(42, new ToolCall { ArgumentsJson = "{\"value\":42}" }.ArgumentsAsJsonElement().GetProperty("value").GetInt32());

    [Theory]
    [InlineData("1e-40", false)]
    [InlineData("1.000000000000000000000000000001", false)]
    [InlineData("1e100", true)]
    [InlineData("1200e-2", true)]
    public void IntegerType_DoesNotRoundFractions(string arguments, bool valid)
        => Assert.Equal(valid, Validate(arguments, """{"type":"integer"}""").IsValid);

    [Fact]
    public void NumericBounds_RejectLossyDecimalConversion()
        => Assert.False(Validate("1e-40", """{"maximum":0}""").IsValid);

    private static ValidationResult Validate(string arguments, string schema)
        => ToolCallValidator.Validate(new() { Name = "test", ArgumentsJson = arguments }, new()
        {
            Name = "test",
            Description = "test",
            Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(schema)!
        });
}
