using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NaderGorge.Infrastructure.Services.AdminAI;

internal static class AdminAIActionInputValidator
{
    private static readonly HashSet<string> SupportedKeywords =
    [
        "type", "properties", "required", "additionalProperties", "items", "enum", "const",
        "format", "pattern", "minLength", "maxLength", "minimum", "maximum",
        "exclusiveMinimum", "exclusiveMaximum", "minItems", "maxItems", "uniqueItems",
        "minProperties", "maxProperties", "description", "title"
    ];
    private static readonly HashSet<string> CommonKeywords = ["type", "enum", "const", "description", "title"];

    public static void Validate(JsonElement input, string schemaJson)
    {
        using var schema = JsonDocument.Parse(schemaJson, new JsonDocumentOptions { MaxDepth = 16 });
        ValidateSchemaTree(schema.RootElement, 0);
        ValidateNode(input, schema.RootElement, "input", 0);
    }

    private static void ValidateSchemaTree(JsonElement schema, int depth)
    {
        if (depth > 16 || schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Admin AI action input schema is invalid.");
        var type = typeValue.GetString();
        if (schema.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
            != schema.EnumerateObject().Count())
            throw new InvalidOperationException("Admin AI action schema contains duplicate keywords.");
        foreach (var keyword in schema.EnumerateObject())
            if (!SupportedKeywords.Contains(keyword.Name))
                throw new InvalidOperationException($"Unsupported Admin AI action schema keyword '{keyword.Name}'.");
        ValidateKeywordsForType(schema, type);
        if (type == "string")
        {
            if (schema.TryGetProperty("format", out var format)
                && (format.ValueKind != JsonValueKind.String
                    || format.GetString() is not ("uuid" or "date-time" or "uri")))
                throw new InvalidOperationException("Unsupported Admin AI action string format.");
            if (schema.TryGetProperty("pattern", out var pattern))
            {
                if (pattern.ValueKind != JsonValueKind.String)
                    throw new InvalidOperationException("Admin AI action string pattern is invalid.");
                try { _ = new Regex(pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException ex) { throw new InvalidOperationException("Admin AI action string pattern is invalid.", ex); }
            }
        }
        if (type == "object")
        {
            if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object
                || !schema.TryGetProperty("additionalProperties", out var additional) || additional.ValueKind != JsonValueKind.False)
                throw new InvalidOperationException("Admin AI action objects require closed properties.");
            if (properties.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count()
                != properties.EnumerateObject().Count())
                throw new InvalidOperationException("Admin AI action schema contains duplicate properties.");
            foreach (var property in properties.EnumerateObject())
                ValidateSchemaTree(property.Value, depth + 1);
            if (schema.TryGetProperty("required", out var required))
            {
                if (required.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("Admin AI action required fields must be an array.");
                if (required.EnumerateArray().Where(field => field.ValueKind == JsonValueKind.String)
                    .Select(field => field.GetString()!).Distinct(StringComparer.Ordinal).Count()
                    != required.GetArrayLength())
                    throw new InvalidOperationException("Admin AI action required fields contain duplicates.");
                foreach (var field in required.EnumerateArray())
                    if (field.ValueKind != JsonValueKind.String || !properties.TryGetProperty(field.GetString()!, out _))
                        throw new InvalidOperationException("Admin AI action required field is not declared.");
            }
        }
        if (type == "array")
        {
            if (!schema.TryGetProperty("items", out var itemSchema) || itemSchema.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Admin AI action arrays require an item schema.");
            ValidateSchemaTree(itemSchema, depth + 1);
        }
    }

    private static void ValidateNode(JsonElement value, JsonElement schema, string path, int depth)
    {
        if (depth > 16 || schema.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Admin AI action input schema is invalid.");
        foreach (var keyword in schema.EnumerateObject())
            if (!SupportedKeywords.Contains(keyword.Name))
                throw new InvalidOperationException($"Unsupported Admin AI action schema keyword '{keyword.Name}'.");
        if (!schema.TryGetProperty("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Admin AI action input schema requires a single type.");
        var type = typeValue.GetString();
        ValidateKeywordsForType(schema, type);
        if (!MatchesType(value, type))
            throw new ArgumentException($"Action input field '{path}' has the wrong type.", nameof(value));

        if (schema.TryGetProperty("enum", out var allowed))
        {
            if (allowed.ValueKind != JsonValueKind.Array || !allowed.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
                throw new ArgumentException($"Action input field '{path}' is outside its allowed values.", nameof(value));
        }
        if (schema.TryGetProperty("const", out var requiredValue) && !JsonElement.DeepEquals(requiredValue, value))
            throw new ArgumentException($"Action input field '{path}' does not match its required value.", nameof(value));

        switch (type)
        {
            case "object": ValidateObject(value, schema, path, depth); break;
            case "array": ValidateArray(value, schema, path, depth); break;
            case "string": ValidateString(value.GetString()!, schema, path); break;
            case "integer":
            case "number": ValidateNumber(value, schema, path); break;
        }
    }

    private static void ValidateKeywordsForType(JsonElement schema, string? type)
    {
        var allowed = new HashSet<string>(CommonKeywords, StringComparer.Ordinal);
        string[] additional = type switch
        {
            "object" => ["properties", "required", "additionalProperties", "minProperties", "maxProperties"],
            "array" => ["items", "minItems", "maxItems", "uniqueItems"],
            "string" => ["format", "pattern", "minLength", "maxLength"],
            "integer" or "number" => ["minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum"],
            "boolean" or "null" => [],
            _ => throw new InvalidOperationException("Unsupported Admin AI action input type.")
        };
        allowed.UnionWith(additional);
        foreach (var property in schema.EnumerateObject())
            if (!allowed.Contains(property.Name))
                throw new InvalidOperationException($"Admin AI action schema keyword '{property.Name}' does not apply to {type}.");
    }

    private static void ValidateObject(JsonElement value, JsonElement schema, string path, int depth)
    {
        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("additionalProperties", out var additional) || additional.ValueKind != JsonValueKind.False)
            throw new InvalidOperationException("Admin AI action objects require closed properties.");
        var supplied = value.EnumerateObject().ToArray();
        if (supplied.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != supplied.Length)
            throw new ArgumentException($"Action input field '{path}' contains duplicate names.", nameof(value));
        CheckCount(supplied.Length, schema, "minProperties", "maxProperties", path);
        foreach (var property in supplied)
        {
            if (!properties.TryGetProperty(property.Name, out var propertySchema))
                throw new ArgumentException($"Unknown action input field '{path}.{property.Name}'.", nameof(value));
            ValidateNode(property.Value, propertySchema, $"{path}.{property.Name}", depth + 1);
        }
        if (!schema.TryGetProperty("required", out var required)) return;
        if (required.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Admin AI action required fields must be an array.");
        foreach (var field in required.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.String || !properties.TryGetProperty(field.GetString()!, out _))
                throw new InvalidOperationException("Admin AI action required field is not declared.");
            if (!value.TryGetProperty(field.GetString()!, out _))
                throw new ArgumentException($"Required action input field '{path}.{field.GetString()}' is missing.", nameof(value));
        }
    }

    private static void ValidateArray(JsonElement value, JsonElement schema, string path, int depth)
    {
        if (!schema.TryGetProperty("items", out var itemSchema) || itemSchema.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Admin AI action arrays require an item schema.");
        var items = value.EnumerateArray().ToArray();
        CheckCount(items.Length, schema, "minItems", "maxItems", path);
        if (schema.TryGetProperty("uniqueItems", out var unique))
        {
            if (unique.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidOperationException("Admin AI action uniqueItems must be boolean.");
            if (unique.ValueKind == JsonValueKind.True)
                for (var i = 0; i < items.Length; i++)
                    for (var j = i + 1; j < items.Length; j++)
                        if (JsonElement.DeepEquals(items[i], items[j]))
                            throw new ArgumentException($"Action input field '{path}' contains duplicate items.", nameof(value));
        }
        for (var i = 0; i < items.Length; i++)
            ValidateNode(items[i], itemSchema, $"{path}[{i}]", depth + 1);
    }

    private static void ValidateString(string value, JsonElement schema, string path)
    {
        CheckCount(value.EnumerateRunes().Count(), schema, "minLength", "maxLength", path);
        if (schema.TryGetProperty("format", out var format))
        {
            if (format.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Admin AI action string format is invalid.");
            var valid = format.GetString() switch
            {
                "uuid" => Guid.TryParseExact(value, "D", out _),
                "date-time" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
                "uri" => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https",
                _ => throw new InvalidOperationException("Unsupported Admin AI action string format.")
            };
            if (!valid) throw new ArgumentException($"Action input field '{path}' has an invalid format.", nameof(value));
        }
        if (schema.TryGetProperty("pattern", out var pattern))
        {
            if (pattern.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Admin AI action string pattern is invalid.");
            try
            {
                if (!Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                    throw new ArgumentException($"Action input field '{path}' does not match its pattern.", nameof(value));
            }
            catch (RegexMatchTimeoutException)
            {
                throw new ArgumentException($"Action input field '{path}' could not be validated within its time limit.", nameof(value));
            }
        }
    }

    private static void ValidateNumber(JsonElement value, JsonElement schema, string path)
    {
        if (!value.TryGetDecimal(out var number))
            throw new ArgumentException($"Action input field '{path}' is outside the supported numeric range.", nameof(value));
        foreach (var (keyword, inclusive, lower) in new[]
        {
            ("minimum", true, true), ("maximum", true, false),
            ("exclusiveMinimum", false, true), ("exclusiveMaximum", false, false)
        })
        {
            if (!schema.TryGetProperty(keyword, out var bound)) continue;
            if (bound.ValueKind != JsonValueKind.Number || !bound.TryGetDecimal(out var limit))
                throw new InvalidOperationException("Admin AI action numeric bound is invalid.");
            var valid = lower ? (inclusive ? number >= limit : number > limit) : (inclusive ? number <= limit : number < limit);
            if (!valid) throw new ArgumentException($"Action input field '{path}' is outside its numeric bounds.", nameof(value));
        }
    }

    private static void CheckCount(int count, JsonElement schema, string minimum, string maximum, string path)
    {
        foreach (var (keyword, lower) in new[] { (minimum, true), (maximum, false) })
        {
            if (!schema.TryGetProperty(keyword, out var bound)) continue;
            if (bound.ValueKind != JsonValueKind.Number || !bound.TryGetInt32(out var limit) || limit < 0)
                throw new InvalidOperationException("Admin AI action size bound is invalid.");
            if (lower ? count < limit : count > limit)
                throw new ArgumentException($"Action input field '{path}' is outside its size bounds.", nameof(count));
        }
    }

    private static bool MatchesType(JsonElement value, string? type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => throw new InvalidOperationException("Unsupported Admin AI action input type.")
    };
}
