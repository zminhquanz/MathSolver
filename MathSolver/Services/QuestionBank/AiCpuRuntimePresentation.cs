namespace MathSolver.Services.QuestionBank;

public sealed record AiCpuRuntimeDisplay(string Title,string Details);

public static class AiCpuRuntimePresentation
{
    public static AiCpuRuntimeDisplay Format(AiCpuRuntimeReport? report,bool modelLoaded,bool vietnamese)
    {
        string Text(string vi,string en)=>vietnamese?vi:en;
        if(report is null)return new("Runtime CPU LLamaSharp",Text(
            "Chưa khởi tạo. Thông tin sẽ cập nhật sau khi nạp model AI.",
            "Not initialized. Information updates after loading an AI model."));
        string title="Runtime CPU LLamaSharp"+(modelLoaded?"":Text(" (lần nạp gần nhất)"," (last model load)"))+": "+report.Backend;
        if(report.InstructionSet.Length>0)title+=" · "+report.InstructionSet;
        var lines=new List<string>();
        lines.Add(report.Features.Count>0
            ?Text("SIMD bản native báo cáo: ","Native SIMD features: ")+string.Join(" · ",report.Features)
            :Text("Bản native chưa báo cáo tập lệnh SIMD.","The native backend has not reported SIMD features."));
        if(report.KernelSelection.Length>0)
            lines.Add(Text("Kernel KleidiAI đã chọn: ","Selected KleidiAI kernels: ")+report.KernelSelection);
        else if(report.KleidiAIAvailable)
            lines.Add(Text("Bản native tích hợp KleidiAI; chưa xác nhận kernel cho tensor model.",
                "KleidiAI is built in; model tensor kernel selection is not confirmed."));
        if(report.UnreportedArmFeatures.Length>0)
            lines.Add(Text("Chưa được runtime báo cáo: ","Not reported by the runtime: ")+report.UnreportedArmFeatures);
        return new(title,string.Join("\n",lines));
    }
}
