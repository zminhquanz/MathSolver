using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static partial class OpenProseTests
{
    private static IEnumerable<BasicQuestionContract> Matrix()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        foreach(var group in QuestionLearningProfile.Groups())
        foreach(var operation in Enum.GetValues<ArithmeticOperation>())
        {
            var profile=new QuestionLearningProfile(group);
            foreach(var scene in AppliedQuestionCatalogue.Available(profile,operation,tier).Take(2))
            {
                var c=AppliedQuestionCatalogue.Create(profile,operation,tier,language,new(713),scene.Id);
                if(OpenQuestionProse.Supports(c))yield return c;
            }
            foreach(var scene in FindXQuestionCatalogue.Available(profile,operation,tier).GroupBy(s=>s.Role).Select(g=>g.First()))
                yield return FindXQuestionCatalogue.Create(profile,operation,tier,language,new(713),scene.Role,scene.Id);
            if(ExpandedArithmeticCatalogue.Supports(group,tier))
                foreach(var style in Enum.GetValues<PracticeArithmeticStyle>())
                {
                    string? context=null;
                    if(style==PracticeArithmeticStyle.Mental && group!=QuestionKnowledgeGroup.Money)
                    {
                        if(operation is not (ArithmeticOperation.Add or ArithmeticOperation.Subtract))continue;
                        context=QuizContentCatalog.LoadList<PracticeCountContext>("PracticeArithmeticContexts",QuizContentCatalog.Culture(language))
                            .FirstOrDefault(c=>c.Group==group && c.AllowsMentalLargeNumbers)?.Id;
                        if(context is null)continue;
                    }
                    var c=ExpandedArithmeticCatalogue.Create(profile,operation,tier,language,new(713),contextId:context,style:style);
                    if(OpenQuestionProse.Supports(c))yield return c;
                }
        }
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        foreach(var type in new[]{ElementaryQuizType.LengthConversion,ElementaryQuizType.MassConversion,ElementaryQuizType.CapacityConversion,
            ElementaryQuizType.AreaConversion,ElementaryQuizType.VolumeConversion})
            yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement,(int)type,tier,language,new(713));
    }

    internal static async Task MatrixAsync()
    {
        using var workspace=TestWorkspace.Temporary("OpenProseMatrix");
        var store=new QuestionBankStore(workspace.FilePath("verified.db3"));
        int profiles=0,reviewed=0;
        foreach(var source in Matrix())
        {
            var c=source with {OpenProse=true};
            var example=OpenQuestionProse.Example(c);string raw=QuestionBankStore.SerializeDraft(example);
            var syntax=OpenQuestionProse.ValidateSyntax(raw,c);
            Check(syntax.IsValid,$"Pilot schema rejected a C# reference: {c.Version}/{c.SceneId}/{c.BankVariant}/{c.Tier}/{c.Language}: {syntax.ErrorCode}/{syntax.ErrorDetails}");
            Check(BasicQuestionValidator.Validate(raw,c).IsValid,"Existing reviewed prose changed under the pilot.");
            string intro=c.Language==AppLanguage.Vietnamese?"Trong lần kiểm kê, ":"While checking supplies, ";
            var novel=example.Facts is null?example with {GivenA=intro+example.GivenA}:example with {Facts=example.Facts.Select((f,i)=>i==0?f with {Text=intro+f.Text}:f).ToArray()};
            var validation=BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(novel),c);
            Check(validation.IsValid,$"Equivalent surface transformation failed: {c.SceneId}/{c.Language}/{validation.ErrorDetails}");
            CheckPractice(c,novel);
            var unknown=Unverified(c);
            var pending=BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(unknown),c);
            Check(pending.NeedsReview && !pending.IsValid,"Unknown language content was automatically trusted.");
            if(profiles%73==0)
            {
                var record=new ValidatedBankQuestion(c,novel,QuestionBankStore.SerializeDraft(novel),"matrix",DateTime.UtcNow);
                bool inserted=await store.InsertAsync(record);
                Check(inserted || await store.ContainsProseAsync(record),"Verified novel prose could not persist.");
                reviewed++;
            }
            string grammar=GgufQuestionRuntime.BuildNovelGrammar(c,OpenQuestionProse.Examples(c).Select(d=>QuestionProseIdentity.Hash(c,d)).ToHashSet());
            Check(grammar.Contains("character ::=") && grammar.All(ch=>ch<=127),"Native open grammar is not portable ASCII.");
            profiles++;
        }
        foreach(var (label,original) in Cases())
        {
            var c=original with {OpenProse=true};var d=OpenQuestionProse.Example(c);
            var reversed=d.Facts is null?d with {GivenA=d.GivenA+" không đúng"}:d with {Facts=d.Facts.Select((f,i)=>i==0?f with {Text=f.Text+" not true"}:f).ToArray()};
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(reversed),c).IsValid,"Negated facts were automatically verified.");
        }
        Console.WriteLine($"PASS {profiles} bilingual pilot profiles across operations, unknown roles, units, stars and groups; fresh C# grading, novel verified prose, review-only unknowns, exhausted references and {reviewed} SQLite checks.");
    }
}
