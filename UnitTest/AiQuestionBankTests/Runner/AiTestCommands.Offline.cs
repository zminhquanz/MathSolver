using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text;
using System.Text.Json;
using static BankDraftFixture;

internal static partial class AiTestCommands
{
    private static IEnumerable<TestCommand> Offline() =>
    [
        new(["--motion-open","--motion-open-model"],"--motion-open | --motion-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await MotionOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,MotionOpenTests.NativeCases());
        },args=>args is ["--motion-open"] or ["--motion-open-model",_] or ["--motion-open-model",_,_]),
        new(["--unitary-groups-open","--unitary-groups-open-model"],"--unitary-groups-open | --unitary-groups-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await UnitaryGroupsOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,UnitaryGroupsOpenTests.NativeCases());
        },args=>args is ["--unitary-groups-open"] or ["--unitary-groups-open-model",_] or ["--unitary-groups-open-model",_,_]),
        new(["--unitary-total-open","--unitary-total-open-model"],"--unitary-total-open | --unitary-total-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await UnitaryTotalOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,UnitaryTotalOpenTests.NativeCases());
        },args=>args is ["--unitary-total-open"] or ["--unitary-total-open-model",_] or ["--unitary-total-open-model",_,_]),
        new(["--difference-ratio-open","--difference-ratio-open-model"],"--difference-ratio-open | --difference-ratio-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await DifferenceRatioOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,DifferenceRatioOpenTests.NativeCases());
        },args=>args is ["--difference-ratio-open"] or ["--difference-ratio-open-model",_] or ["--difference-ratio-open-model",_,_]),
        new(["--sum-ratio-open","--sum-ratio-open-model"],"--sum-ratio-open | --sum-ratio-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await SumRatioOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,SumRatioOpenTests.NativeCases());
        },args=>args is ["--sum-ratio-open"] or ["--sum-ratio-open-model",_] or ["--sum-ratio-open-model",_,_]),
        new(["--sum-difference-open","--sum-difference-open-model"],"--sum-difference-open | --sum-difference-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await SumDifferenceOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,SumDifferenceOpenTests.NativeCases());
        },args=>args is ["--sum-difference-open"] or ["--sum-difference-open-model",_] or ["--sum-difference-open-model",_,_]),
        new(["--average-missing-open","--average-missing-open-model"],"--average-missing-open | --average-missing-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await AverageMissingOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,AverageMissingOpenTests.NativeCases());
        },args=>args is ["--average-missing-open"] or ["--average-missing-open-model",_] or ["--average-missing-open-model",_,_]),
        new(["--calendar-february-leap-open","--calendar-february-leap-open-model"],"--calendar-february-leap-open | --calendar-february-leap-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await FebruaryLeapCalendarTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,FebruaryLeapCalendarTests.NativeCases());
        },args=>args is ["--calendar-february-leap-open"] or ["--calendar-february-leap-open-model",_] or ["--calendar-february-leap-open-model",_,_]),
        new(["--calendar-february-common-open","--calendar-february-common-open-model"],"--calendar-february-common-open | --calendar-february-common-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await FebruaryCommonCalendarTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,FebruaryCommonCalendarTests.NativeCases());
        },args=>args is ["--calendar-february-common-open"] or ["--calendar-february-common-open-model",_] or ["--calendar-february-common-open-model",_,_]),
        new(["--calendar-adjacent-inclusive-open","--calendar-adjacent-inclusive-open-model"],"--calendar-adjacent-inclusive-open | --calendar-adjacent-inclusive-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await AdjacentMonthInclusiveTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,AdjacentMonthInclusiveTests.NativeCases());
        },args=>args is ["--calendar-adjacent-inclusive-open"] or ["--calendar-adjacent-inclusive-open-model",_] or ["--calendar-adjacent-inclusive-open-model",_,_]),
        new(["--calendar-adjacent-open","--calendar-adjacent-open-model"],"--calendar-adjacent-open | --calendar-adjacent-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await AdjacentMonthCalendarTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,AdjacentMonthCalendarTests.NativeCases());
        },args=>args is ["--calendar-adjacent-open"] or ["--calendar-adjacent-open-model",_] or ["--calendar-adjacent-open-model",_,_]),
        new(["--calendar-inclusive-open","--calendar-inclusive-open-model"],"--calendar-inclusive-open | --calendar-inclusive-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await SameMonthInclusiveTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,SameMonthInclusiveTests.NativeCases());
        },args=>args is ["--calendar-inclusive-open"] or ["--calendar-inclusive-open-model",_] or ["--calendar-inclusive-open-model",_,_]),
        new(["--calendar-open","--calendar-open-model"],"--calendar-open | --calendar-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await SameMonthCalendarTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,SameMonthCalendarTests.NativeCases());
        },args=>args is ["--calendar-open"] or ["--calendar-open-model",_] or ["--calendar-open-model",_,_]),
        new(["--overnight-open","--overnight-open-model"],"--overnight-open | --overnight-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await OvernightTimeTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,OvernightTimeTests.NativeCases());
        },args=>args is ["--overnight-open"] or ["--overnight-open-model",_] or ["--overnight-open-model",_,_]),
        new(["--time-break-open","--time-break-open-model"],"--time-break-open | --time-break-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await SameDayBreakTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,SameDayBreakTests.NativeCases());
        },args=>args is ["--time-break-open"] or ["--time-break-open-model",_] or ["--time-break-open-model",_,_]),
        new(["--rectangle-area-open","--rectangle-area-open-model"],"--rectangle-area-open | --rectangle-area-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await RectangleAreaOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,RectangleAreaOpenTests.NativeCases());
        },args=>args is ["--rectangle-area-open"] or ["--rectangle-area-open-model",_] or ["--rectangle-area-open-model",_,_]),
        new(["--rectangle-perimeter-open","--rectangle-perimeter-open-model"],"--rectangle-perimeter-open | --rectangle-perimeter-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await RectanglePerimeterOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,RectanglePerimeterOpenTests.NativeCases());
        },args=>args is ["--rectangle-perimeter-open"] or ["--rectangle-perimeter-open-model",_] or ["--rectangle-perimeter-open-model",_,_]),
        new(["--percentage-remaining-english","--percentage-remaining-english-model"],"--percentage-remaining-english | --percentage-remaining-english-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await PercentageRemainingEnglishTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,
                PercentageRemainingOpenTests.NativeCases().Where(x=>x.Contract.Language==AppLanguage.English));
        },args=>args is ["--percentage-remaining-english"] or ["--percentage-remaining-english-model",_] or ["--percentage-remaining-english-model",_,_]),
        new(["--percentage-part-open","--percentage-part-open-model"],"--percentage-part-open | --percentage-part-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await PercentagePartOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,PercentagePartOpenTests.NativeCases());
        },args=>args is ["--percentage-part-open"] or ["--percentage-part-open-model",_] or ["--percentage-part-open-model",_,_]),
        new(["--open-prose-audit"],"--open-prose-audit <directory> (compare old evidence with changed source; not an unchanged replay)",
            args=>OpenProseTests.VerifyEvidenceAsync(args[1],auditChangedSource:true),args=>args is ["--open-prose-audit",_]),
        new(["--average-open","--average-open-model"],"--average-open | --average-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await AverageOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,AverageOpenTests.NativeCases());
        },args=>args is ["--average-open"] or ["--average-open-model",_] or ["--average-open-model",_,_]),
        new(["--percentage-remaining-open","--percentage-remaining-open-model"],"--percentage-remaining-open | --percentage-remaining-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await PercentageRemainingOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,PercentageRemainingOpenTests.NativeCases());
        },args=>args is ["--percentage-remaining-open"] or ["--percentage-remaining-open-model",_] or ["--percentage-remaining-open-model",_,_]),
        new(["--percentage-whole-open","--percentage-whole-open-model"],"--percentage-whole-open | --percentage-whole-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await PercentageWholeOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,PercentageWholeOpenTests.NativeCases());
        },args=>args is ["--percentage-whole-open"] or ["--percentage-whole-open-model",_] or ["--percentage-whole-open-model",_,_]),
        new(["--fraction-whole-open","--fraction-whole-open-model"],"--fraction-whole-open | --fraction-whole-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await FractionWholeOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,FractionWholeOpenTests.NativeCases());
        },args=>args is ["--fraction-whole-open"] or ["--fraction-whole-open-model",_] or ["--fraction-whole-open-model",_,_]),
        new(["--fraction-remaining-open","--fraction-remaining-open-model"],"--fraction-remaining-open | --fraction-remaining-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await FractionRemainingOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,FractionRemainingOpenTests.NativeCases());
        },args=>args is ["--fraction-remaining-open"] or ["--fraction-remaining-open-model",_] or ["--fraction-remaining-open-model",_,_]),
        new(["--fraction-part-open","--fraction-part-open-model"],"--fraction-part-open | --fraction-part-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await FractionPartOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,FractionPartOpenTests.NativeCases());
        },args=>args is ["--fraction-part-open"] or ["--fraction-part-open-model",_] or ["--fraction-part-open-model",_,_]),
        new(["--time-open","--time-open-model"],"--time-open | --time-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await SameDayTimeTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,SameDayTimeTests.NativeCases());
        },args=>args is ["--time-open"] or ["--time-open-model",_] or ["--time-open-model",_,_]),
        new(["--decimal-open","--decimal-open-model"],"--decimal-open | --decimal-open-model <gguf> [directory]",async args=>
        {
            if(args.Length==1)await DecimalOpenTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null,DecimalOpenTests.NativeCases());
        },args=>args is ["--decimal-open"] or ["--decimal-open-model",_] or ["--decimal-open-model",_,_]),
        new(["--open-prose-evidence"], "--open-prose-evidence <directory>", args => OpenProseTests.VerifyEvidenceAsync(args[1]),
            args => args is ["--open-prose-evidence", _]),
        new(["--open-prose", "--open-prose-model"], "--open-prose | --open-prose-model <model.gguf> [directory]", async args =>
        {
            if (args.Length == 1) await OpenProseTests.RunAsync();
            else await OpenProseTests.RunModelAsync(args[1],args.Length>2?args[2]:null);
        }, args => args is ["--open-prose"] or ["--open-prose-model", _] or ["--open-prose-model", _, _]),
        new(["--applied-narratives-evidence"], "--applied-narratives-evidence <directory>", args=>AppliedNarrativeBankTests.VerifyEvidenceAsync(args[1]),
            args=>args is ["--applied-narratives-evidence",_]),
        new(["--applied-narratives", "--applied-narratives-model"], "--applied-narratives | --applied-narratives-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await AppliedNarrativeBankTests.RunAsync();
            else await AppliedNarrativeBankTests.RunModelAsync(args[1]);
        }, args => args is ["--applied-narratives"] or ["--applied-narratives-model", _]),
        new(["--regex-timeout", "--seconds-regex-model"], "--regex-timeout | --seconds-regex-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await RegexTimeoutRegressionTests.RunAsync();
            else await RegexTimeoutRegressionTests.RunModelBatchAsync(args[1]);
        }, args => args is ["--regex-timeout"] or ["--seconds-regex-model", _]),
        new(["--decimal-spatial-evidence"], "--decimal-spatial-evidence <directory>", args => DecimalSpatialBankTests.VerifyEvidenceAsync(args[1]),
            args => args is ["--decimal-spatial-evidence", _]),
        new(["--decimal-spatial", "--decimal-spatial-model"], "--decimal-spatial | --decimal-spatial-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await DecimalSpatialBankTests.RunAsync();
            else await DecimalSpatialBankTests.RunModelAsync(args[1]);
        }, args => args is ["--decimal-spatial"] or ["--decimal-spatial-model", _]),
        new(["--time-fraction", "--time-fraction-model"], "--time-fraction | --time-fraction-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await TimeFractionBankTests.RunAsync();
            else await TimeFractionBankTests.RunModelAsync(args[1]);
        }, args => args is ["--time-fraction"] or ["--time-fraction-model", _]),
        new(["--expanded-arithmetic", "--expanded-arithmetic-model"], "--expanded-arithmetic | --expanded-arithmetic-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await ExpandedArithmeticTests.RunAsync();
            else await ExpandedArithmeticTests.RunModelAsync(args[1]);
        }, args => args is ["--expanded-arithmetic"] or ["--expanded-arithmetic-model", _]),
        new(["--harness"], "--harness (runner, workspaces, model teardown and worker lifecycle; no inference)", async args =>
        {
            await HarnessInfrastructureTests.RunAsync();
            await ModelHarnessTests.RunAsync();
            await WorkerLifecycleTests.RunAsync();
        }, args => args is ["--harness"]),
        new(["--generation-lifecycle"], "--generation-lifecycle (stop, streaming and model lifecycle; no inference)", async args =>
        {
            await WorkerLifecycleTests.RunAsync();
            await StreamingTests.RunAsync(Draft);
            await MemoryLifecycleTests.RunAsync();
        }, args => args is ["--generation-lifecycle"]),
        new(["--area-measurement-foundation"], "--area-measurement-foundation", args =>
        {
            AreaMeasurementFoundationTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--area-measurement-foundation"]),
        new(["--equivalent-prose"], "--equivalent-prose", async args =>
        {
            await EquivalentProseTests.RunAsync();
        }, args => args is ["--equivalent-prose"]),
        new(["--excel-authoring"], "--excel-authoring", async args =>
        {
            await ExcelAuthoringTests.RunAsync();
        }, args => args is ["--excel-authoring"]),
        new(["--next-ai", "--next-ai-model"], "--next-ai | --next-ai-model <model.gguf> [part]", async args =>
        {
            if (args.Length == 1) await NextAiFormsTests.RunAsync(); else await NextAiFormsTests.RunModelAsync(args[1], args.Length == 3 ? args[2] : null);
        }, args => args is ["--next-ai"] or ["--next-ai-model", _] or ["--next-ai-model", _, _]),
        new(["--activity-depth", "--activity-depth-model", "--activity-depth-packing-english-model"], "--activity-depth | --activity-depth-model <model.gguf> | --activity-depth-packing-english-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await ActivityDepthTests.RunAsync();
            else await ActivityDepthTests.RunModelAsync(args[1], args[0] == "--activity-depth-packing-english-model");
        }, args => args is ["--activity-depth"] or ["--activity-depth-model", _] or ["--activity-depth-packing-english-model", _]),
        new(["--quantity-depth", "--quantity-depth-model"], "--quantity-depth | --quantity-depth-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await QuantityDepthTests.RunAsync();
            else await QuantityDepthTests.RunModelAsync(args[1]);
        }, args => args is ["--quantity-depth"] or ["--quantity-depth-model", _]),
        new(["--narrative-depth", "--narrative-depth-model", "--legacy-prose-evidence", "--narrative-depth-evidence"], "--narrative-depth | --narrative-depth-model <model.gguf> | --legacy-prose-evidence <directory> | --narrative-depth-evidence <directory>", async args =>
        {
            if (args.Length == 1) await NarrativeDepthTests.RunAsync();
            else if (args[0] is "--legacy-prose-evidence" or "--narrative-depth-evidence")
                        NarrativeDepthTests.CheckDatabase(args[1], legacy: args[0] == "--legacy-prose-evidence");
            else await ProseExpansionTests.RunModelAsync(args[1], narrativeDepth: true);
        }, args => args is ["--narrative-depth"] or ["--narrative-depth-model", _] or ["--legacy-prose-evidence", _] or ["--narrative-depth-evidence", _]),
        new(["--prose-expansion", "--prose-expansion-model"], "--prose-expansion | --prose-expansion-model <model.gguf>", async args =>
        {
            if (args.Length == 1) await ProseExpansionTests.RunAsync();
            else await ProseExpansionTests.RunModelAsync(args[1]);
        }, args => args is ["--prose-expansion"] or ["--prose-expansion-model", _]),
        new(["--structure-policy"], "--structure-policy", args =>
        {
            GenerationStructurePolicyTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--structure-policy"]),
        new(["--decimal-bank"], "--decimal-bank", async args =>
        {
            await DecimalBankTests.RunAsync();
        }, args => args is ["--decimal-bank"]),
        new(["--packing-time-bank"], "--packing-time-bank", async args =>
        {
            await PackingTimeBankTests.RunAsync();
        }, args => args is ["--packing-time-bank"]),
        new(["--fraction-quantity-bank"], "--fraction-quantity-bank", async args =>
        {
            await FractionQuantityBankTests.RunAsync();
        }, args => args is ["--fraction-quantity-bank"]),
        new(["--measurement-bank"], "--measurement-bank", async args =>
        {
            await MeasurementBankTests.RunAsync();
        }, args => args is ["--measurement-bank"]),
        new(["--proportion-bank"], "--proportion-bank", async args =>
        {
            await ProportionBankTests.RunAsync();
        }, args => args is ["--proportion-bank"]),
        new(["--motion-bank"], "--motion-bank", async args =>
        {
            await MotionBankTests.RunAsync();
        }, args => args is ["--motion-bank"]),
        new(["--multistep-bank"], "--multistep-bank", async args =>
        {
            await MultiStepBankTests.RunAsync();
        }, args => args is ["--multistep-bank"]),
        new(["--reasoning-stories"], "--reasoning-stories", async args =>
        {
            await ReasoningStoryTests.RunAsync();
        }, args => args is ["--reasoning-stories"]),
        new(["--quiz-content"], "--quiz-content", args =>
        {
            QuizContentTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--quiz-content"]),
        new(["--arithmetic-contexts"], "--arithmetic-contexts", async args =>
        {
            using var workspace = TestWorkspace.Temporary("arithmetic-contexts");
            string contextDirectory = workspace.DirectoryPath;
                 await ArithmeticContextTests.RunAsync(contextDirectory);
        }, args => args is ["--arithmetic-contexts"]),
        new(["--one-step-practice"], "--one-step-practice", args =>
        {
            OneStepPracticeTests.Run();
            return Task.CompletedTask;
        }, args => args is ["--one-step-practice"]),
        new(["--practice-formats"], "--practice-formats", async args =>
        {
            using var workspace = TestWorkspace.Temporary("practice-formats");
            string folder = workspace.DirectoryPath;
                 await PracticeFormatTests.RunAsync(folder);
        }, args => args is ["--practice-formats"]),
        new(["--fraction-bank"], "--fraction-bank", async args =>
        {
            using var workspace = TestWorkspace.Temporary("fraction-bank");
            string folder = workspace.DirectoryPath;
                 await FractionBankTests.RunAsync(folder);
        }, args => args is ["--fraction-bank"]),
        new(["--findx-bank"], "--findx-bank", async args =>
        {
            using var workspace = TestWorkspace.Temporary("findx-bank");
            string folder = workspace.DirectoryPath;
                 await FindXBankTests.RunAsync(folder);
        }, args => args is ["--findx-bank"]),
        new(["--clear-bank"], "--clear-bank", async args =>
        {
            using var workspace = TestWorkspace.Temporary("clear-bank");
            string folder = workspace.DirectoryPath;
                 await DeleteAllTests.RunAsync(folder);
        }, args => args is ["--clear-bank"]),
        new(["--sql-grid"], "--sql-grid", async args =>
        {
            using var workspace = TestWorkspace.Temporary("sql-grid");
            string folder = workspace.DirectoryPath;

            var contract = BasicQuestionContract.Create(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(43));
            string raw = Draft(contract);
            var draft = BasicQuestionValidator.Validate(raw, contract).Draft!;
            await SqlGridTests.RunAsync(folder, new(contract, draft, raw, "grid-test", DateTime.UtcNow));
        }, args => args is ["--sql-grid"]),
        new(["--bank-data"], "--bank-data", async args =>
        {
            using var workspace = TestWorkspace.Temporary("bank-data");
            var contract = BasicQuestionContract.Create(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(43));
            string raw = Draft(contract);
            var draft = BasicQuestionValidator.Validate(raw, contract).Draft!;
            var sample = new ValidatedBankQuestion(contract, draft, raw, "data-test", DateTime.UtcNow);
            await BankDataTests.RunAsync(workspace.DirectoryPath, sample, Draft);
            await SqlMutationTests.RunAsync(workspace.DirectoryPath, sample);
            await DeleteAllTests.RunAsync(workspace.DirectoryPath);
        }, args => args is ["--bank-data"]),
        new(["--prose-deduplication"], "--prose-deduplication", async args =>
        {
            using var workspace = TestWorkspace.Temporary("prose-deduplication");
            string folder = workspace.DirectoryPath;
            await ProseDeduplicationTests.RunAsync(folder);
        }, args => args is ["--prose-deduplication"]),
        new(["--learning-profiles"], "--learning-profiles", async args =>
        {
            using var workspace = TestWorkspace.Temporary("learning-profiles");
            string folder = workspace.DirectoryPath;
            await LearningProfileTests.RunAsync(folder);
        }, args => args is ["--learning-profiles"]),
        new(["--addition-tests"], "--addition-tests", async args =>
        {
            using var workspace = TestWorkspace.Temporary("addition-tests");
            string testDirectory = workspace.DirectoryPath;
            await AdditionTests.RunAsync(testDirectory);
        }, args => args is ["--addition-tests"]),
    ];
}
