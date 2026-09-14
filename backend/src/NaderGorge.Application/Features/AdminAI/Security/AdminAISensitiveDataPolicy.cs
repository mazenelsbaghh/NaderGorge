using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NaderGorge.Application.Features.AdminAI.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Security;

public sealed class AdminAISensitiveDataPolicy : IAdminAISensitiveDataPolicy
{
    private const string Redacted = "[REDACTED]";
    private static readonly string[] ProhibitedFragments =
    [
        "password", "passwordhash", "token", "tokenhash", "refreshtoken", "secret", "apikey",
        "encryptionkey", "privatekey", "connectionstring", "cookie", "session", "fingerprint",
        "verificationcode", "verificationanswer", "parenttrackingcode", "payrolldetail", "nonce", "credential"
    ];

    private static readonly HashSet<Type> ScalarTypes =
    [
        typeof(string), typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double),
        typeof(decimal), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
        typeof(DateOnly), typeof(TimeOnly)
    ];

    public string PolicyHash { get; } = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join('\n', ProhibitedFragments.Order(StringComparer.Ordinal)))));

    public void AssertSafeSchema(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Inspect(type, new HashSet<Type>(), type.Name);
    }

    public string RedactJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        JsonNode? node;
        try { node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }); }
        catch (JsonException exception) { throw new ArgumentException("A valid bounded JSON document is required.", nameof(json), exception); }
        RedactNode(node);
        return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "null";
    }

    private static void Inspect(Type type, ISet<Type> visited, string path)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum || ScalarTypes.Contains(type)) return;
        if (type == typeof(byte[]) || typeof(System.Security.SecureString).IsAssignableFrom(type))
            throw new InvalidOperationException($"Prohibited Admin AI schema type at '{path}'.");
        if (TryGetEnumerableElement(type, out var element)) { Inspect(element, visited, path + "[]"); return; }
        if (!visited.Add(type)) return;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (IsProhibited(property.Name))
                throw new InvalidOperationException($"Prohibited Admin AI schema field '{path}.{property.Name}'.");
            Inspect(property.PropertyType, visited, path + "." + property.Name);
        }
    }

    private static bool TryGetEnumerableElement(Type type, out Type element)
    {
        if (type.IsArray) { element = type.GetElementType()!; return true; }
        var enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        element = enumerable?.GetGenericArguments()[0] ?? typeof(object);
        return enumerable is not null;
    }

    private static void RedactNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                if (IsProhibited(key)) obj[key] = Redacted;
                else RedactNode(obj[key]);
            }
        }
        else if (node is JsonArray array)
            foreach (var child in array) RedactNode(child);
    }

    private static bool IsProhibited(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return ProhibitedFragments.Any(normalized.Contains);
    }
}
