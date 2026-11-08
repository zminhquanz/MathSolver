using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Testing;

internal static class RemainingSubjectArtworkTests
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void Run()
    {
        int count=0;var assets=new HashSet<string>();
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var actors=QuizStoryArtworkCatalogue.MotionActors(language);
            foreach(var sample in RemainingSubjectFixtures.Cases(language))
            {
                var q=sample.Question;var before=System.Text.Json.JsonSerializer.Serialize(q);
                string? id=q.MotionProblem is {} motion
                    ? QuizStoryArtwork.SelectMotion(motion.NarrativeActors,actors,true)
                    : QuizStoryArtwork.Select(true,false,[],artworkId:QuizStoryArtworkCatalogue.SourceArtwork(q,language));
                Check(id==$"quiz_story_{sample.Artwork}.png","Missing source binding: "+language+"/"+sample.Name);
                Check(System.Text.Json.JsonSerializer.Serialize(q)==before,"Artwork changed frozen question");assets.Add(sample.Artwork);count++;
                if(q.ElementaryProblem?.DataChart is {} chart)
                {
                    var changed=q with {ElementaryProblem=q.ElementaryProblem with {DataChart=chart with {Profile=chart.Profile with {QuestionKind=DataChartQuestionKind.Total}}}};
                    Check(QuizStoryArtworkCatalogue.SourceArtwork(changed,language) is null,"A total must not borrow one chart category");
                    changed=q with {ElementaryProblem=q.ElementaryProblem with {Kind=QuizProblemKind.Time}};
                    Check(QuizStoryArtworkCatalogue.SourceArtwork(changed,language) is null,"Wrong kind must not borrow a chart picture");
                }
                if(q.WordProblem?.PracticeDetails is {} details)
                {
                    var changed=q with {WordProblem=q.WordProblem with {PracticeDetails=details with {Purchases=details.Purchases.Select(p=>p with {ItemName="unknown product"}).ToArray()}}};
                    Check(QuizStoryArtworkCatalogue.SourceArtwork(changed,language) is null,"Product must match the authored item, not just its ID");
                    Check(QuizStoryArtwork.Select(true,false,["đồng"],isMoney:true)=="quiz_story_money.png","Generic money still uses wallet");
                }
            }
            foreach(var actor in actors.Where(a=>a.Name is "xe giao hàng" or "delivery vehicle"))
                Check(QuizStoryArtwork.SelectMotion([actor.Name],actors,false) is null,"Vague delivery vehicle must not turn into a truck");
        }
        Check(assets.Count==22,"Not every new asset has a real source fixture: "+string.Join(',',assets));
        Console.WriteLine($"PASS remaining subjects: {count} real VI/EN C# questions, all22 new identities, motion/measurement/proportion/chart/purchase ownership and frozen-fact guards.");
    }
}
