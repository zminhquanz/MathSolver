using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal sealed class ReasoningStoryCycle
{
    private readonly Dictionary<(BankQuestionFamily, int, CurriculumTier, AppLanguage), Queue<string>> _recent = [];
    private readonly Dictionary<(CurriculumTier,AppLanguage),int> _motionRotation = [];
    private readonly Dictionary<(CurriculumTier,AppLanguage),int> _sideRotation = [];
    private readonly Dictionary<(CurriculumTier,AppLanguage),int> _retailRotation = [];
    private readonly Dictionary<(int,CurriculumTier,AppLanguage),int> _extendedMotionRotation = [];

    public BasicQuestionContract Next(BankQuestionFamily family, int variant, CurriculumTier tier, AppLanguage language,
        IReadOnlySet<string>? excludedProse = null, bool creativeProse = false)
    {
        var key = (family, variant, tier, language);
        var practicalProfiles=creativeProse?PracticalOpenCatalogue.Profiles(family,variant,language)
            .Concat(AppliedOpenCatalogue.Profiles(family,variant,language)).ToArray():[];
        if(creativeProse&&family==BankQuestionFamily.MultiStep&&variant==MoneyChangePilot.Variant)
        {
            _retailRotation.TryGetValue((tier,language),out int cursor);
            string prefix=cursor%2==0?MoneyChangePilot.Prefix:DiscountOpenPilot.Prefix;
            _retailRotation[(tier,language)]=(cursor+1)%2;
            practicalProfiles=practicalProfiles.Where(p=>p.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
        }
        if(practicalProfiles.Length>0&&family==BankQuestionFamily.Spatial&&variant==RectangleSidePilot.Variant)
        {
            _sideRotation.TryGetValue((tier,language),out int cursor);
            string target=cursor%2==0?"length":"width";
            _sideRotation[(tier,language)]=(cursor+1)%2;
            practicalProfiles=practicalProfiles.Where(p=>p.StartsWith(RectangleSidePilot.Prefix+target+"/",StringComparison.Ordinal)).ToArray();
        }
        string? motionRelation=null;
        string? extendedRelation=null;
        if(creativeProse&&family==BankQuestionFamily.Motion&&variant is (int)MotionQuizType.Meeting or (int)MotionQuizType.Chasing or (int)MotionQuizType.River)
        {
            var relations=MotionExtendedPilot.Relations(language).Where(r=>(int)r.Type==variant).OrderBy(r=>r.Kind).ToArray();
            var rotationKey=(variant,tier,language);
            _extendedMotionRotation.TryGetValue(rotationKey,out int cursor);
            extendedRelation=relations[cursor%relations.Length].Id;
            _extendedMotionRotation[rotationKey]=(cursor+1)%relations.Length;
        }
        if(creativeProse&&family==BankQuestionFamily.Motion&&variant==(int)MotionQuizType.Basic)
        {
            var relations=MotionOpenPilot.Relations(language).OrderBy(r=>r.Kind).ToArray();
            _motionRotation.TryGetValue((tier,language),out int cursor);
            motionRelation=relations[cursor%relations.Length].Id;
            _motionRotation[(tier,language)]=(cursor+1)%relations.Length;
        }
        if (!_recent.TryGetValue(key, out var history)) _recent[key] = history = new();
        BasicQuestionContract? best = null;
        int bestCount = int.MaxValue;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var profiles=practicalProfiles.Length>0?practicalProfiles:extendedRelation is not null
                ?MotionExtendedPilot.Profiles(language,(MotionQuizType)variant).Where(id=>id.StartsWith(MotionExtendedPilot.Prefix+extendedRelation+"/",StringComparison.Ordinal)).ToArray()
                :creativeProse && family==BankQuestionFamily.Motion&&variant==(int)MotionQuizType.Basic
                ? MotionOpenPilot.Profiles(language).Where(id=>id.StartsWith(MotionOpenPilot.Prefix+motionRelation+"/",StringComparison.Ordinal)).ToArray()
                : creativeProse && family==BankQuestionFamily.Decimal
                ? DecimalStoryPilot.Profiles((ElementaryQuizType)variant,language).ToArray()
                : family==BankQuestionFamily.Proportion && variant==(int)ProportionQuizType.UnitaryGroupCount
                    ? UnitaryGroupsPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.Proportion && variant==(int)ProportionQuizType.Direct
                    ? UnitaryTotalPilot.Profiles(language).ToArray()
                : family==BankQuestionFamily.Time&&SameMonthCalendarPilot.IsType((ElementaryQuizType)variant)
                    ? SameMonthCalendarPilot.ProfilesForType(language,variant).ToArray()
                : family==BankQuestionFamily.Time&&variant==(int)OvernightTimePilot.Type
                    ? OvernightTimePilot.Profiles(language).ToArray()
                : family==BankQuestionFamily.Time&&variant==(int)SameDayBreakPilot.Type
                    ? SameDayBreakPilot.Profiles(language).ToArray()
                : family==BankQuestionFamily.Time&&SameDayTimePilot.IsType((ElementaryQuizType)variant)
                    ? SameDayTimePilot.Profiles((ElementaryQuizType)variant,language).ToArray()
                : family==BankQuestionFamily.FractionQuantity && variant==(int)ElementaryQuizType.FractionRemaining
                    ? FractionRemainingPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.FractionQuantity && variant==(int)ElementaryQuizType.WholeFromFraction
                    ? FractionWholePilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.FractionQuantity && variant==(int)ElementaryQuizType.FractionOfNumber
                    ? FractionPartPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.Geometry && variant==RectangleAreaPilot.Variant
                    ? RectangleAreaPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.Geometry && variant==RectanglePerimeterPilot.Variant
                    ? RectanglePerimeterPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.TwoNumbers && variant==(int)ElementaryQuizType.SumRatio
                    ? SumRatioPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.TwoNumbers && variant==(int)ElementaryQuizType.DifferenceRatio
                    ? DifferenceRatioPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.TwoNumbers && variant==(int)ElementaryQuizType.SumDifference
                    ? SumDifferencePilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.Average && variant==(int)AverageQuizType.MissingValue
                    ? AverageMissingPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.Average && variant==(int)AverageQuizType.Direct
                    ? AverageOpenPilot.Profiles(language).ToArray()
                : family==BankQuestionFamily.Percentage && variant==(int)PercentageQuizType.FindRemainingAfterDonation
                    ? PercentageRemainingPilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.Percentage && variant==(int)PercentageQuizType.FindWholeFromPercentageValue
                    ? PercentageWholePilot.Profiles(language).ToArray()
                : creativeProse && family==BankQuestionFamily.Percentage && variant==(int)PercentageQuizType.FindPercentageValue
                    ? PercentagePartPilot.Profiles(language).ToArray():[];
            var profile=profiles.OrderBy(id=>history.Count(s=>s.StartsWith(id+"/",StringComparison.Ordinal)))
                .ThenBy(_=>Random.Shared.Next()).FirstOrDefault()??"";
            var c = ReasoningStoryCatalogue.Create(family, variant, tier, language,narrativeId:profile);
            string context = c.SceneId + "/" + c.Unit;
            int repetitions = history.Count(s => s == context);
            if (repetitions < bestCount) { best = c; bestCount = repetitions; }
            if (repetitions == 0) break;
        }
        if (family is BankQuestionFamily.Proportion or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity or BankQuestionFamily.Spatial && excludedProse is not null
            && !(creativeProse && OpenQuestionProse.Supports(best!)) && !ReviewedReasoningProse.NovelDrafts(best!, excludedProse).Any())
        {
            // One exhausted context must not stop a batch while other compatible
            // contexts still have wording. Search the finite catalog before failing.
            var narrativeIds = family switch
            {
                _ when ElementaryQuizGenerator.IsAppliedNarrative((ElementaryQuizType)variant) && family is BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Time or BankQuestionFamily.Spatial
                    => new[] { "applied-" + (ElementaryQuizType)variant },
                BankQuestionFamily.Decimal or BankQuestionFamily.Spatial when ElementaryQuizGenerator.IsDecimalSpatialSkill((ElementaryQuizType)variant)
                    => ElementaryQuizGenerator.DecimalSpatialContexts((ElementaryQuizType)variant),
                BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity when ElementaryQuizGenerator.IsTimeFractionExtension((ElementaryQuizType)variant)
                    => ElementaryQuizGenerator.TimeFractionContextIds((ElementaryQuizType)variant),
                BankQuestionFamily.Decimal => ElementaryQuizGenerator.DecimalContexts(language).Select(c => c.Id),
                BankQuestionFamily.Measurement when variant == (int)ElementaryQuizType.MapScale =>
                    ElementaryQuizGenerator.MapScaleContexts(language)
                        .Where(c => (int)tier >= c.MinimumStar && (int)tier <= c.MaximumStar).Select(c => c.Id),
                BankQuestionFamily.Measurement => ElementaryQuizGenerator.MeasurementContexts(language, (ElementaryQuizType)variant).Select(c => c.Id),
                BankQuestionFamily.Remainder => ElementaryQuizGenerator.PackingStories(language).Select(c => c.Id),
                BankQuestionFamily.Time when SameMonthCalendarPilot.IsType((ElementaryQuizType)variant)=>SameMonthCalendarPilot.ProfilesForType(language,variant),
                BankQuestionFamily.Time when variant==(int)OvernightTimePilot.Type=>OvernightTimePilot.Profiles(language),
                BankQuestionFamily.Time when variant==(int)SameDayBreakPilot.Type=>SameDayBreakPilot.Profiles(language),
                BankQuestionFamily.Time when SameDayTimePilot.IsType((ElementaryQuizType)variant)=>SameDayTimePilot.Profiles((ElementaryQuizType)variant,language),
                BankQuestionFamily.Time => ElementaryQuizGenerator.TimeStoryContextIds(tier),
                BankQuestionFamily.FractionQuantity when variant==(int)ElementaryQuizType.FractionRemaining => FractionRemainingPilot.Profiles(language),
                BankQuestionFamily.FractionQuantity => FractionQuantityStoryContextCatalog.GetProfile(language).Select(c => c.ContextId),
                _ => ProportionQuizGenerator.NarrativeIds((ProportionQuizType)variant, tier, language)
            };
            best = narrativeIds
                .Select(id => ReasoningStoryCatalogue.Create(family, variant, tier, language, narrativeId: id))
                .Where(c => ReviewedReasoningProse.NovelDrafts(c, excludedProse).Any())
                .OrderBy(c => history.Count(s => s == c.SceneId + "/" + c.Unit)).FirstOrDefault()
                ?? throw new InvalidOperationException("DuplicateProseRetriesExhausted");
        }
        if (ReasoningStoryCatalogue.UsesReviewedPhrasings(family) && excludedProse is not null
            && !(creativeProse && OpenQuestionProse.Supports(best!))
            && !ReviewedReasoningProse.NovelDrafts(best!, excludedProse).Any())
        {
            // Resampling is bounded and never calls inference. A used-up scene
            // should not stop generation while other compatible scenes remain.
            best = Enumerable.Range(0, 256).Select(_ => ReasoningStoryCatalogue.Create(family, variant, tier, language))
                .FirstOrDefault(c => ReviewedReasoningProse.NovelDrafts(c, excludedProse).Any())
                ?? throw new InvalidOperationException("DuplicateProseRetriesExhausted");
        }
        history.Enqueue(best!.SceneId + "/" + best.Unit);
        if (history.Count > 4) history.Dequeue();
        return best;
    }
}
