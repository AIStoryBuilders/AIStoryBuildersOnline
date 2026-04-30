using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace AIStoryBuilders.AI
{
    /// <summary>
    /// Rebuilds tool parameter schemas into the minimal subset Google's Gemini
    /// <c>parametersJsonSchema</c> validator accepts. Wraps each
    /// <see cref="AIFunction"/> with a sanitised version that exposes only
    /// <c>type / properties / required</c> at the root, with primitive property
    /// schemas only.
    /// </summary>
    internal static class GeminiToolSanitizer
    {
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "string", "number", "integer", "boolean", "array", "object"
        };

        public static IList<AITool> SanitizeForGemini(IList<AITool> tools)
        {
            if (tools == null || tools.Count == 0) return tools;
            var result = new List<AITool>(tools.Count);
            foreach (var t in tools)
            {
                if (t is AIFunction fn)
                {
                    try
                    {
                        var safeSchema = BuildMinimalSchema(fn.JsonSchema);
                        result.Add(new MinimalSchemaFunction(fn, safeSchema));
                    }
                    catch
                    {
                        // If sanitization fails for any reason, fall back to a
                        // permissive empty-object schema so the tool name and
                        // description are still callable.
                        var fallback = JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new { },
                            required = Array.Empty<string>()
                        });
                        result.Add(new MinimalSchemaFunction(fn, fallback));
                    }
                }
                else
                {
                    result.Add(t);
                }
            }
            return result;
        }

        private static JsonElement BuildMinimalSchema(JsonElement source)
        {
            var root = new JsonObject
            {
                ["type"] = "object"
            };

            var properties = new JsonObject();
            var required = new JsonArray();

            if (source.ValueKind == JsonValueKind.Object)
            {
                if (source.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in props.EnumerateObject())
                    {
                        properties[prop.Name] = BuildPropertySchema(prop.Value);
                    }
                }

                if (source.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array)
                {
                    foreach (var name in req.EnumerateArray())
                    {
                        if (name.ValueKind == JsonValueKind.String)
                            required.Add(name.GetString());
                    }
                }
            }

            root["properties"] = properties;
            root["required"] = required;

            return JsonSerializer.SerializeToElement(root);
        }

        private static JsonNode BuildPropertySchema(JsonElement source)
        {
            var type = ResolvePrimitiveType(source);

            var schema = new JsonObject { ["type"] = type };

            // Preserve description if present (string only).
            if (source.ValueKind == JsonValueKind.Object
                && source.TryGetProperty("description", out var desc)
                && desc.ValueKind == JsonValueKind.String)
            {
                schema["description"] = desc.GetString();
            }

            switch (type)
            {
                case "string":
                    if (source.ValueKind == JsonValueKind.Object
                        && source.TryGetProperty("enum", out var en)
                        && en.ValueKind == JsonValueKind.Array)
                    {
                        var enumArr = new JsonArray();
                        foreach (var v in en.EnumerateArray())
                        {
                            if (v.ValueKind == JsonValueKind.String)
                                enumArr.Add(v.GetString());
                        }
                        if (enumArr.Count > 0)
                        {
                            schema["enum"] = enumArr;
                            schema["format"] = "enum";
                        }
                    }
                    break;
                case "array":
                    JsonNode itemsNode = null;
                    if (source.ValueKind == JsonValueKind.Object
                        && source.TryGetProperty("items", out var items))
                    {
                        if (items.ValueKind == JsonValueKind.Object)
                        {
                            itemsNode = BuildPropertySchema(items);
                        }
                    }
                    schema["items"] = itemsNode ?? new JsonObject { ["type"] = "string" };
                    break;
                case "object":
                    schema["properties"] = new JsonObject();
                    break;
            }

            return schema;
        }

        private static string ResolvePrimitiveType(JsonElement source)
        {
            if (source.ValueKind != JsonValueKind.Object) return "string";

            if (source.TryGetProperty("type", out var t))
            {
                if (t.ValueKind == JsonValueKind.String)
                {
                    var s = t.GetString();
                    if (!string.IsNullOrEmpty(s) && AllowedTypes.Contains(s))
                        return s.ToLowerInvariant();
                }
                else if (t.ValueKind == JsonValueKind.Array)
                {
                    // Pick the first allowed non-null type.
                    foreach (var v in t.EnumerateArray())
                    {
                        if (v.ValueKind == JsonValueKind.String)
                        {
                            var s = v.GetString();
                            if (!string.Equals(s, "null", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrEmpty(s)
                                && AllowedTypes.Contains(s))
                            {
                                return s.ToLowerInvariant();
                            }
                        }
                    }
                }
            }

            // enum without explicit type => string
            if (source.TryGetProperty("enum", out _)) return "string";

            // anything unknown / oneOf / $ref / nullable collapses to string
            return "string";
        }

        private sealed class MinimalSchemaFunction : AIFunction
        {
            private readonly AIFunction _inner;
            private readonly JsonElement _schema;

            public MinimalSchemaFunction(AIFunction inner, JsonElement schema)
            {
                _inner = inner;
                _schema = schema;
            }

            public override string Name => _inner.Name;
            public override string Description => _inner.Description;
            public override JsonElement JsonSchema => _schema;
            public override JsonSerializerOptions JsonSerializerOptions => _inner.JsonSerializerOptions;

            protected override ValueTask<object> InvokeCoreAsync(
                AIFunctionArguments arguments,
                CancellationToken cancellationToken)
            {
                return _inner.InvokeAsync(arguments, cancellationToken);
            }
        }
    }
}
