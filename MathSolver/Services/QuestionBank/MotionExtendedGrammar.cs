using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal static class MotionExtendedGrammar
{
    internal static string Build(BasicQuestionContract c)
    {
        using var document=JsonDocument.Parse(QuestionBankStore.SerializeDraft(OpenQuestionProse.Example(c)));
        int factCount=document.RootElement.GetProperty("facts").GetArrayLength();
        var rules=new StringBuilder();int index=0;
        string L(string value)=>JsonSerializer.Serialize(value);
        IEnumerable<string[]> Orders(string[] blocks)
        {
            if(blocks.Length==0){yield return [];yield break;}
            for(int i=0;i<blocks.Length;i++)
                foreach(var tail in Orders(blocks.Where((_,j)=>j!=i).ToArray()))yield return [blocks[i],..tail];
        }
        string Node(JsonElement node,string property="")
        {
            if(node.ValueKind==JsonValueKind.Object)return L("{")+" ws "+string.Join(" ws "+L(",")+" ws ",node.EnumerateObject().Select(p=>L(JsonSerializer.Serialize(p.Name))+" ws "+L(":")+" ws "+Node(p.Value,p.Name)))+" ws "+L("}");
            if(node.ValueKind==JsonValueKind.Array)return L("[")+" ws "+string.Join(" ws "+L(",")+" ws ",node.EnumerateArray().Select(x=>Node(x)))+" ws "+L("]");
            string text=node.GetString()!;
            if(property is "role" or "step" or "unit_id")return L(JsonSerializer.Serialize(text));
            string name="field-"+index++;
            var matches=Regex.Matches(text,@"\{[fv]\d+\}").Cast<Match>().ToArray();
            var blocks=new List<string>();
            for(int i=0;i<matches.Length;i++)
            {
                string block=matches[i].Value;
                if(block.StartsWith("{f",StringComparison.Ordinal)&&i+1<matches.Length&&matches[i+1].Value.StartsWith("{v",StringComparison.Ordinal)
                    &&string.IsNullOrWhiteSpace(text[(matches[i].Index+matches[i].Length)..matches[i+1].Index]))
                    block+=" "+matches[++i].Value;
                blocks.Add(L(block));
            }
            // Preserve role slots and numeric-unit pairs; every surrounding
            // predicate and block order is still generated, not a sentence list.
            string body=blocks.Count==0?"character{1,260}":"("+string.Join(" | ",Orders(blocks.ToArray()).Select(order=>
                "(character{1,95} "+L(" ")+")? "+string.Join(" gap ",order)+" tail"))+")";
            // All clause text fields precede the single question; headings follow it.
            int fieldIndex=index-1;
            string ending=fieldIndex<factCount?".":property=="question"?"?":"";
            rules.AppendLine(name+" ::= "+L("\"")+" "+body+(ending.Length>0?" "+L(ending):"")+" "+L("\""));
            return name;
        }
        string root=Node(document.RootElement);
        return "root ::= "+root+" ws\n"+rules+"character ::= ["+QuestionProseLanguage.GrammarLetters(c.Language)+" ,'\\u2019()-]\n"
            +"gap ::= \" \" (character{1,95} \" \")?\ntail ::= (\" \" character{1,180} | \", \" character{1,180})?\nws ::= [ \\t\\n\\r]*\n";
    }
}
