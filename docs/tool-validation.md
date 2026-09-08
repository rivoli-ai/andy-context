# Tool argument validation

The BCL validator implements a subset of [JSON Schema](https://json-schema.org/understanding-json-schema/reference/object). It validates arguments before execution in both orchestration modes and never fetches remote schemas.

Supported constraints:

- type: object, array, string, number, integer, boolean, null; a list permits multiple types.
- properties, required, additionalProperties (boolean or nested schema).
- items (one schema for every array item), minItems, maxItems.
- enum, const, minimum, maximum, minLength, maxLength.
- Nested object/boolean schemas; string length counts Unicode scalar values.

title, description, default, examples and $schema are accepted as annotations. Defaults are not inserted; $schema does not select another validator.

Unknown keywords, including $ref, allOf, anyOf, oneOf, pattern, format and tuple-array keywords, fail explicitly even within an unused property schema. Malformed supported constraints also fail. This is not a full JSON Schema implementation. Integer checks use exact decimal notation. Numeric bounds require exact .NET decimal representation; out-of-range or precision-losing conversions fail explicitly. Length/count constraints use nonnegative 32-bit integers.

Arguments must be nonempty valid JSON. Scalar roots are allowed unless an object type is required. Extra properties are allowed unless constrained. Required fields and property types are independent checks. Errors include property paths; orchestration sends a validation_failed result and does not execute the tool.
