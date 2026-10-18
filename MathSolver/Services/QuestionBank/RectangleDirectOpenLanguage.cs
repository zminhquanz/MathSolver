using MathSolver.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Proves only the shape and side roles; each relation proves its own target and units.</summary>
internal static class RectangleDirectOpenLanguage
{
    private static readonly ConcurrentDictionary<string,Regex> Patterns=new(StringComparer.Ordinal);
    private static readonly Regex Whitespace=QuestionTextRegex.Create(@"\s+");
    internal static bool Has(string text,string pattern)=>Patterns.GetOrAdd(pattern,static p=>QuestionTextRegex.Create("(?i)"+p)).IsMatch(text);
    internal static bool Match(string text,string pattern)=>Has(Whitespace.Replace(text.Trim()," ").TrimEnd('.', '?', ':', '!').Trim(),@"\A(?:"+pattern+@")\z");
    internal static bool Shape(string text,AppLanguage language)=>language==AppLanguage.Vietnamese
        ?Match(text,@"\{v0\} (?:có dạng|có hình dạng|có hình dáng|mang hình dáng|là một|là) \{v1\}")
            ||Match(text,@"một \{v0\} (?:có dạng|có hình dạng|là) \{v1\}")
        :Match(text,@"\{v0\} (?:is|is shaped as|is shaped like) (?:a )?\{v1\}")
            ||Match(text,@"\{v0\} has (?:the|a) shape of a \{v1\}");
    internal static bool Dimension(string text,string role,string amount,AppLanguage language)=>language==AppLanguage.Vietnamese
        ?Match(text,@"\{v0\} có chiều "+role+@" (?:(?:là|bằng) )?"+amount+@" \{v2\}")
            ||Match(text,@"chiều "+role+@" (?:của )?\{v0\} (?:là|bằng|đo được) "+amount+@" \{v2\}")
            // Both direct rectangle pilots guarantee length > width. Only
            // the width role may therefore be called the shorter side.
            ||Match(text,@"cạnh "+(role=="rộng"?"(?:rộng|ngắn)":role)+@" của \{v0\} (?:là|bằng|đo được) "+amount+@" \{v2\}")
            ||Match(text,@"\{v0\} có cạnh "+(role=="rộng"?"(?:rộng|ngắn)":role)+@" (?:(?:là|bằng) )?"+amount+@" \{v2\}")
            ||Match(text,@"\{v0\} "+role+" "+amount+@" \{v2\}")
        :Match(text,@"\{v0\} has a "+role+@" of "+amount+@" \{v2\}")
            ||Match(text,@"the "+role+@" of \{v0\} (?:is|measures|equals) "+amount+@" \{v2\}")
            ||Match(text,@"\{v0\} (?:is|measures) "+amount+@" \{v2\} "+(role=="length"?"long":"wide"))
            ||Match(text,@"\{v0\} measures "+amount+@" \{v2\} in "+role)
            ||Match(text,@"in "+role+@", \{v0\} measures "+amount+@" \{v2\}")
            ||Match(text,@"\{v0\}['’]s "+role+@" (?:is|measures|equals) "+amount+@" \{v2\}");
}
