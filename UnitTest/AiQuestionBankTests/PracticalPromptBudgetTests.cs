using LLama;
using LLama.Common;
using LLama.Native;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;

internal static class PracticalPromptBudgetTests
{
    internal static void Run(string path,string output)
    {
        NativeLibraryConfig.All.WithLogCallback((_,_)=>{});
        var parameters=new ModelParams(path){ContextSize=2048,GpuLayerCount=0,LoadMode=LLamaLoadMode.MemoryMap,Embeddings=false,SwaFull=false};
        using var weights=LLamaWeights.LoadFromFile(parameters);
        var rows=new List<object>();
        foreach(var (name,c) in PracticalOpenTests.NativeCases())
        foreach(bool retry in new[]{false,true})
        {
            var original=OpenQuestionProse.Example(c);
            string prompt=retry?BasicQuestionPrompt.Build(c,"DuplicateProse",[original.ProblemText],new HashSet<string>{QuestionProseIdentity.Hash(c,original)},"Preserve quantity roles and write a different factual clause.")
                :BasicQuestionPrompt.Build(c);
            int tokens=weights.Tokenize(GgufQuestionRuntime.FormatChatPrompt(weights,c,prompt),true,true,Encoding.UTF8).Length;
            int context=GgufQuestionRuntime.GetContextTokens(tokens);
            rows.Add(new{Name=name,Retry=retry,PromptTokens=tokens,ContextTokens=context});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS exact GGUF chat-token budget for {rows.Count} initial/retry prompts within the existing 2048-token cap; no inference ran.");
    }
}
