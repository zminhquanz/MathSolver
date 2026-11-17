using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

internal static class FoodThemeInterchangeTests
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    private static readonly string[] WesternSteakIds =
    [
        "steak-bo", "steak-ga", "steak-cuu", "steak-bo-sot-tieu", "steak-ga-sot-kem-nam",
        "steak-bo-sot-ruou-vang-do", "steak-bo-sot-bearnaise", "steak-cuu-sot-chimichurri",
        "steak-ga-sot-bbq", "steak-cuu-sot-bo-toi"
    ];
    private static readonly string[] KoreanFoodIds =
    [
        "tteokbokki", "korean-corn-dog", "hotteok", "bungeoppang", "gyeranppang",
        "sundae-gukbap", "andong-jjimdak", "kalguksu", "squid-sundae"
    ];
    private static readonly string[] VietnameseFoodIds =
    [
        "com-suon-cay", "com-chien-hai-san", "com-chien-duong-chau",
        "com-ga-xoi-mo", "com-ga-sot-me", "com-ga-sot-tieu-xanh", "com-ga-mam-toi",
        "canh-chua-ca-loc", "canh-kho-qua-nhoi-thit", "canh-bi-do-ham-xuong",
        "canh-cua-rau-day-mong-toi", "canh-rau-ngot-thit-bam", "ca-loc-kho-to"
    ];
    private static readonly string[] DessertShopIds =
    [
        "kem-bo-da-lat", "kem-trang-tien", "sua-chua-ha-long"
    ];
    internal static byte[] ActivateExample(byte[] bytes,AppLanguage language)
    {
        using var stream=new MemoryStream();stream.Write(bytes);
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Update,true))
        {
            var part=zip.GetEntry("xl/worksheets/sheet2.xml")!;XElement sheet;using(var read=part.Open())sheet=XElement.Load(read);
            XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            sheet.Descendants(ns+"c").Single(c=>(string?)c.Attribute("r")=="A2").Descendants(ns+"t").Single().Value=language==AppLanguage.Vietnamese?"Nhập":"Import";
            part.Delete();using var write=zip.CreateEntry("xl/worksheets/sheet2.xml").Open();sheet.Save(write);
        }
        return stream.ToArray();
    }
    internal static BasicQuestionContract Contract(FoodThemeCatalogue.Subject subject,ArithmeticOperation op,AppLanguage language,BankQuestionFamily family)
    {
        var tier=CurriculumTier.ThreeStars;var profile=new QuestionLearningProfile(subject.Group);
        string id=$"theme-{subject.Group.ToString().ToLowerInvariant()}-{subject.Unit.Id}-"+(op switch{ArithmeticOperation.Add=>"total",ArithmeticOperation.Subtract=>"remaining",ArithmeticOperation.Multiply=>"groups",_=>"share"});
        if(family==BankQuestionFamily.Fraction)return FractionQuestionCatalogue.Create(profile,op,tier,language,new(15),"fraction-"+id);
        if(family==BankQuestionFamily.FindX)
        {
            var scene=FindXQuestionCatalogue.Available(profile,op,tier).First(s=>s.SourceScene.UnitId==subject.Unit.Id);
            return FindXQuestionCatalogue.Create(profile,op,tier,language,new(17),scene.Role,scene.Id);
        }
        return AppliedQuestionCatalogue.Create(profile,op,tier,language,new(13),id);
    }
    public static async Task RunAsync(string output)
    {
        string folder=Path.GetFullPath(output);Directory.CreateDirectory(folder);int fixtures=0,templates=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var records=new List<ValidatedBankQuestion>();
            foreach(var subject in FoodThemeCatalogue.Subjects)
            foreach(var op in Enum.GetValues<ArithmeticOperation>())
            foreach(var family in new[]{BankQuestionFamily.Arithmetic,BankQuestionFamily.Fraction,BankQuestionFamily.FindX})
            {
                var c=Contract(subject,op,language,family);var d=BasicQuestionTemplates.Example(c);string raw=QuestionBankStore.SerializeDraft(d);
                FoodThemeTests.CheckFoodVenue(c,d);
                FoodThemeTests.CheckDrinkShopVenue(c,d);
                Check(BasicQuestionValidator.Validate(raw,c).IsValid,"Typed source before interchange: "+c.SceneId);
                records.Add(new(c,d,raw,"C# themes",DateTime.UtcNow));fixtures++;
            }
            string culture=QuizContentCatalog.Culture(language);using var backup=new MemoryStream();QuestionBankWorkbook.WriteBackup(backup,records);
            await File.WriteAllBytesAsync(Path.Combine(folder,"food-themes-"+culture+"-backup.xlsx"),backup.ToArray());backup.Position=0;
            var rows=QuestionBankWorkbook.Read(backup);Check(rows.Count==records.Count&&rows.All(r=>r.Question is not null),"Backup lost themed rows: "+string.Join(',',rows.Where(r=>r.Question is null).Select(r=>r.ErrorCode)));
            Check(FoodThemeCatalogue.Subjects.Count(s => s.Unit.Id.StartsWith("steak-",StringComparison.Ordinal)) == WesternSteakIds.Length,
                "Unexpected number of steak subjects in the shared catalogue");
            Check(KoreanFoodIds.All(id => FoodThemeCatalogue.Subjects.Count(s => s.Unit.Id == id) == 1),
                "A Korean dish is missing or duplicated in the shared catalogue");
            Check(VietnameseFoodIds.Length == 13 && VietnameseFoodIds.All(id => FoodThemeCatalogue.Subjects.Count(s => s.Unit.Id == id) == 1),
                "One of the 13 Vietnamese dishes is missing or duplicated in the shared catalogue");
            Check(DessertShopIds.All(id => FoodThemeCatalogue.Subjects.Count(s => s.Unit.Id == id) == 1
                && FoodThemeCatalogue.DessertShopKind(id) is not null),
                "One of the ice cream/yogurt shop subjects is missing, duplicated or misclassified");
            foreach (var id in WesternSteakIds.Concat(KoreanFoodIds).Concat(VietnameseFoodIds).Concat(DessertShopIds))
            foreach (var family in new[]{BankQuestionFamily.Arithmetic,BankQuestionFamily.Fraction,BankQuestionFamily.FindX})
            {
                var subject=FoodThemeCatalogue.Subjects.Single(s=>s.Unit.Id==id);
                var contract=Contract(subject,ArithmeticOperation.Add,language,family);
                Check(rows.Any(r=>r.Question?.Contract==contract),"Excel backup lost an exact prepared dish scene: "+id+"/"+family+"/"+language);
                string aiPrompt=BasicQuestionPrompt.Build(contract);
                Check(aiPrompt.Contains(contract.Unit,StringComparison.OrdinalIgnoreCase),
                    "AI prompt lost the exact prepared dish unit: "+id+"/"+family+"/"+language);
                string guard = DessertShopIds.Contains(id)
                    ? language == AppLanguage.Vietnamese ? "hai chủ thể cố định" : "fixed actors"
                    : VietnameseFoodIds.Contains(id)
                    ? language == AppLanguage.Vietnamese ? "Giữ đúng món Việt Nam đã chọn" : "Preserve the exact Vietnamese prepared dish"
                    : KoreanFoodIds.Contains(id)
                    ? language == AppLanguage.Vietnamese ? "không nhầm dồi" : "Never confuse blood sausage"
                    : language == AppLanguage.Vietnamese ? "Giữ đúng thịt bò/gà/cừu" : "Preserve the C# protein";
                Check(aiPrompt.Contains(guard, StringComparison.OrdinalIgnoreCase),
                    "AI prompt lost the food identity guard: "+id+"/"+family+"/"+language);
                if (DessertShopIds.Contains(id))
                {
                    string dessert=FoodThemeCatalogue.DessertShopKind(id)!;
                    var (first,second,both)=FoodThemePrompt.DessertShopActors(dessert,language);
                    Check(contract.Subject.Equals(first,StringComparison.OrdinalIgnoreCase)
                        && contract.OtherSubject.Equals(second,StringComparison.OrdinalIgnoreCase)
                        && aiPrompt.Contains(both,StringComparison.OrdinalIgnoreCase),
                        "AI/Excel dessert shop actor contract changed: "+id+"/"+family+"/"+language);
                }
            }
            for(int i=0;i<rows.Count;i++)
            {
                Check(rows[i].Question!.Contract==records[i].Contract&&rows[i].Question!.Contract.AnswerText==records[i].Contract.AnswerText,"Backup changed subject/group/fractions");
                Check(rows[i].Question!.Draft==records[i].Draft,"Backup changed food venue narrative: "+records[i].Contract.SceneId);
                FoodThemeTests.CheckFoodVenue(rows[i].Question!.Contract,rows[i].Question!.Draft);
                FoodThemeTests.CheckDrinkShopVenue(rows[i].Question!.Contract,rows[i].Question!.Draft);
            }
            // Isolate this run so a previous verification cannot turn fresh inserts into duplicates.
            var store=new QuestionBankStore(Path.Combine(folder,$"themes-{culture}-{Guid.NewGuid():N}.db3"));backup.Position=0;var imported=await store.ImportExcelAsync(backup);
            int unique=records.Select(r=>QuestionProseIdentity.Hash(r.Contract,r.Draft)).Distinct().Count();
            Check(imported.Inserted==unique&&imported.Duplicates==records.Count-unique&&imported.Rejected==0,"SQLite import/dedup mismatch: "+System.Text.Json.JsonSerializer.Serialize(imported));
            foreach(var group in new[]{QuestionKnowledgeGroup.Food,QuestionKnowledgeGroup.Drinks,QuestionKnowledgeGroup.Fruit})
            {
                var profile=new QuestionLearningProfile(group);var tier=CurriculumTier.ThreeStars;
                Check((await store.TakeForProfileAsync(ArithmeticOperation.Add,tier,language,profile))?.Contract.KnowledgeGroup==group,"SQLite integer theme routing");
                Check((await store.TakeFractionAsync(ArithmeticOperation.Add,tier,language,profile))?.Contract.KnowledgeGroup==group,"SQLite fraction theme routing");
                Check((await store.TakeFindXAsync(ArithmeticOperation.Add,tier,language,profile))?.Contract.KnowledgeGroup==group,"SQLite Find X theme routing");
                foreach(var family in new[]{BankQuestionFamily.Arithmetic,BankQuestionFamily.Fraction,BankQuestionFamily.FindX})
                {
                    var c=Contract(FoodThemeCatalogue.Subjects.First(s=>s.Group==group),ArithmeticOperation.Add,language,family);
                    using var authoring=new MemoryStream();QuestionAuthoringWorkbook.Write(authoring,c,group.ToString(),language);
                    var bytes=authoring.ToArray();using(var schema=new MemoryStream(bytes))using(var doc=SpreadsheetDocument.Open(schema,false))
                        Check(!new OpenXmlValidator().Validate(doc).Any(),"Authoring OOXML schema invalid");
                    using(var inactive=new MemoryStream(bytes))Check(QuestionBankWorkbook.Read(inactive).Count==0,"Template example imported without explicit activation");
                    await File.WriteAllBytesAsync(Path.Combine(folder,$"{group}-{family}-{culture}-template.xlsx"),bytes);
                    using var active=new MemoryStream(ActivateExample(bytes,language));var authored=QuestionBankWorkbook.Read(active).Single();
                    Check(authored.Question?.Contract==c&&QuestionBankStore.ValidateSavedQuestion(authored.Question!).IsValid,"Authoring metadata/role/group changed");
                    if(group==QuestionKnowledgeGroup.Food)FoodThemeTests.CheckFoodVenue(authored.Question!.Contract,authored.Question!.Draft);
                    if(group==QuestionKnowledgeGroup.Drinks)FoodThemeTests.CheckDrinkShopVenue(authored.Question!.Contract,authored.Question!.Draft);
                    templates++;
                }
            }
            using var exported=new MemoryStream();await store.ExportExcelAsync(exported);exported.Position=0;
            var reexported=QuestionBankWorkbook.Read(exported);
            Check(reexported.Count==unique,"Re-export lost nonduplicate themed rows");
            foreach (var id in WesternSteakIds.Concat(KoreanFoodIds).Concat(VietnameseFoodIds).Concat(DessertShopIds))
            foreach (var family in new[]{BankQuestionFamily.Arithmetic,BankQuestionFamily.Fraction,BankQuestionFamily.FindX})
            {
                var subject=FoodThemeCatalogue.Subjects.Single(s=>s.Unit.Id==id);
                var contract=Contract(subject,ArithmeticOperation.Add,language,family);
                Check(reexported.Any(r=>r.Question?.Contract==contract),
                    "SQLite/Excel re-export lost prepared dish identity: "+id+"/"+family+"/"+language);
            }
            foreach(var row in reexported)
            {
                Check(row.Question is not null,"Re-export lost a themed record");
                FoodThemeTests.CheckFoodVenue(row.Question!.Contract,row.Question!.Draft);
                FoodThemeTests.CheckDrinkShopVenue(row.Question!.Contract,row.Question!.Draft);
            }
        }
        Console.WriteLine($"PASS food themes interchange: {fixtures} typed records, {templates} VI/EN authoring templates, exact 10 steak/sauce, 9 Korean and {VietnameseFoodIds.Length} Vietnamese dish identities across three families and both languages, AI prompts, OOXML schema, explicit import, backup/SQLite/re-export.");
    }
}
