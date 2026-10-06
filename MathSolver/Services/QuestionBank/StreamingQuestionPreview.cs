using System.Globalization;
using System.Text;

namespace MathSolver.Services.QuestionBank;

/// <summary>Reads display-only prose from incomplete JSON; never validates or saves it.</summary>
public static class StreamingQuestionPreview
{
    private static readonly string[] Fields = ["given_a", "given_b", "question"];

    public static string Read(string? json) => string.Join(" ", Fields.Select(key => ReadField(json, key).Trim()).Where(value => value.Length > 0));

    public static string ReadField(string? json, string field)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        int index = 0;
        SkipWhitespace(json, ref index);
        if (index >= json.Length || json[index++] != '{') return "";
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        while (index < json.Length)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '"') break;
            string key = ReadString(json, ref index, out bool keyComplete);
            if (!keyComplete) break;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index++] != ':') break;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '"') break;
            string value = ReadString(json, ref index, out bool valueComplete);
            values.TryAdd(key, value);
            if (!valueComplete) break;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index++] != ',') break;
        }
        return values.GetValueOrDefault(field, "");
    }

    public static string Render(string? json, BasicQuestionContract c)
    {
        if (!c.IsTemplate) return Read(json);
        var unit = QuestionUnits.Find(ReadField(json, "unit_id"));
        if (unit is not null && c.Version is not (AppliedQuestionCatalogue.Version or FindXQuestionCatalogue.Version or FractionQuestionCatalogue.Version)) c = BasicQuestionTemplates.ApplyUnit(c, unit);
        // A stream may end halfway through a placeholder. Wait for its closing brace.
        string CompleteSlots(string field)
        {
            string text = ReadField(json, field).Trim();
            int unfinished = text.LastIndexOf('{');
            return unfinished >= 0 && text.IndexOf('}', unfinished) < 0 ? text[..unfinished].TrimEnd() : text;
        }
        return BasicQuestionTemplates.RenderProblem(CompleteSlots("given_a"), CompleteSlots("given_b"), CompleteSlots("question"), c);
    }

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
    }

    private static string ReadString(string text, ref int index, out bool complete)
    {
        complete = false;
        index++; // Opening quote was checked by the caller.
        var value = new StringBuilder();
        while (index < text.Length)
        {
            char character = text[index++];
            if (character == '"') { complete = true; break; }
            if (character == '\\')
            {
                if (index == text.Length) break;
                char escape = text[index++];
                if (escape == 'u')
                {
                    if (index + 4 > text.Length || !ushort.TryParse(text.AsSpan(index, 4),
                        NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code)) break;
                    character = (char)code;
                    index += 4;
                }
                else
                {
                    character = escape switch
                    {
                        '"' => '"', '\\' => '\\', '/' => '/', 'b' => '\b', 'f' => '\f',
                        'n' => '\n', 'r' => '\r', 't' => '\t', _ => '\0'
                    };
                    if (character == '\0') break;
                }
            }
            else if (char.IsControl(character)) break;
            value.Append(character);
        }
        // Do not render a surrogate half if the stream splits a Unicode escape/pair.
        if (value.Length > 0 && char.IsHighSurrogate(value[^1])) value.Length--;
        return value.ToString();
    }
}
