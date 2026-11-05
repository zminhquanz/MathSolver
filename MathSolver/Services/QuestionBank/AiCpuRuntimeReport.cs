using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Evidence from the loaded CPU backend, never from host CPU capabilities.</summary>
public sealed record AiCpuRuntimeReport
{
    private static readonly TimeSpan RegexTimeout=TimeSpan.FromMilliseconds(100);
    private static readonly string[] ArmFeatures=["NEON","DotProd","I8MM","SME","SME2","SVE","SVE2"];
    public bool HasCpuFeatures {get;private init;}
    public bool KleidiAIAvailable {get;private init;}
    public bool UsesKleidiAI {get;private init;}
    public string Backend => UsesKleidiAI?"ARM KleidiAI":"CPU";
    public string InstructionSet {get;private init;}="";
    public IReadOnlyList<string> Features {get;private init;}=Array.Empty<string>();
    public string KernelSelection {get;private init;}="";
    public string UnreportedArmFeatures {get;private init;}="";

    public static AiCpuRuntimeReport Create(string systemInfo,IEnumerable<string> diagnostics)
    {
        // llama_print_system_info concatenates registry sections. GPU features
        // must not accidentally upgrade the CPU's compile-time instruction set.
        var cpu=Regex.Matches(systemInfo,@"(?<name>[A-Za-z][A-Za-z0-9_-]*)\s*:\s*(?<body>.*?)(?=[A-Za-z][A-Za-z0-9_-]*\s*:|$)",
            RegexOptions.Singleline|RegexOptions.CultureInvariant,RegexTimeout)
            .Cast<Match>().FirstOrDefault(m=>m.Groups["name"].Value.Equals("CPU",StringComparison.OrdinalIgnoreCase));
        var flags=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(cpu is not null)
            foreach(Match flag in Regex.Matches(cpu.Groups["body"].Value,@"\b([A-Z][A-Z0-9_]*)\s*=\s*1\s*(?=\||$)",
                RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,RegexTimeout))flags.Add(flag.Groups[1].Value);
        bool Has(string key)=>flags.Contains(key);
        var features=new List<string>();
        foreach(string name in new[]{"SSE2","SSE3","SSSE3","AVX","AVX2","AVX512","NEON","DOTPROD","I8MM","SME","SME2","SVE","SVE2"})
            if(Has(name)||(name=="I8MM"&&Has("MATMUL_INT8")))features.Add(Canonical(name));
        string[] logs=diagnostics.ToArray();
        bool packed=logs.Any(text=>Regex.Matches(text,@"\bCPU_KLEIDIAI\s+model buffer size\s*=\s*([0-9.]+)\s+MiB\b",
            RegexOptions.CultureInvariant,RegexTimeout).Cast<Match>().Any(m=>double.TryParse(m.Groups[1].Value,
                NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double size)&&double.IsFinite(size)&&size>0));
        bool kleidi=Has("KLEIDIAI")&&packed;
        var kernels=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if(kleidi)
            foreach(string text in logs)
                foreach(Match kernel in Regex.Matches(text,@"(?:^|\r?\n)\s*kleidiai:\s+primary\s+(q4|q8|f32)\s+kernel feature\s+(SME2|SME|SVE2|SVE|I8MM|DOTPROD|NEON)\b",
                    RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,RegexTimeout))
                    kernels[kernel.Groups[1].Value.ToUpperInvariant()]=Canonical(kernel.Groups[2].Value.ToUpperInvariant());
        string instruction=Has("AVX512")?"AVX-512 (512-bit)":Has("AVX2")?"AVX2 (256-bit)":Has("AVX")?"AVX (256-bit)":
            new[]{"SME2","SME","SVE2","SVE","I8MM","DotProd","NEON","SSSE3","SSE3","SSE2"}.FirstOrDefault(features.Contains)??"";
        return new()
        {
            HasCpuFeatures=cpu is not null&&flags.Count>0,KleidiAIAvailable=Has("KLEIDIAI"),UsesKleidiAI=kleidi,
            Features=features.AsReadOnly(),InstructionSet=instruction,
            KernelSelection=string.Join(" · ",new[]{"Q4","Q8","F32"}.Where(kernels.ContainsKey).Select(key=>$"{key}: {kernels[key]}")),
            UnreportedArmFeatures=features.Any(ArmFeatures.Contains)||Has("KLEIDIAI")
                ?string.Join(" · ",ArmFeatures.Where(name=>!features.Contains(name)&&!kernels.Values.Contains(name))):""
        };
    }

    private static string Canonical(string name)=>name switch {"AVX512"=>"AVX-512","DOTPROD"=>"DotProd",_=>name};
}
