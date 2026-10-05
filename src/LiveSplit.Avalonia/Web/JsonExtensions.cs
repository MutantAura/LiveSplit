using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Xml;

namespace LiveSplit.Web;

/// <summary>
/// Lenient accessors for web APIs' JSON: missing properties and nulls read as defaults.
/// </summary>
internal static class Json
{
    public static JsonElement Obj(this JsonElement e, string name)
    {
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement value) ? value : default;
    }

    public static string Str(this JsonElement e, string name)
    {
        JsonElement value = e.Obj(name);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    public static bool Bool(this JsonElement e, string name)
    {
        return e.Obj(name).ValueKind == JsonValueKind.True;
    }

    public static int? Int(this JsonElement e, string name)
    {
        JsonElement value = e.Obj(name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result) ? result : null;
    }

    public static DateTime? Date(this JsonElement e, string name)
    {
        string value = e.Str(name);
        return value != null && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime result)
            ? result
            : null;
    }

    /// <summary>
    /// Reads an ISO 8601 duration such as "P0DT00H00M15S", as produced by Django.
    /// </summary>
    public static TimeSpan? Duration(this JsonElement e, string name)
    {
        string value = e.Str(name);
        if (value == null)
        {
            return null;
        }

        try
        {
            return XmlConvert.ToTimeSpan(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static IEnumerable<JsonElement> EnumerateArrayOrEmpty(this JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : [];
    }

    public static IReadOnlyList<string> Strings(this JsonElement e, string name)
    {
        JsonElement value = e.Obj(name);
        return value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString())]
            : [];
    }
}
