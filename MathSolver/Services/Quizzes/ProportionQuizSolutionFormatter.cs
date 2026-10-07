using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public static class ProportionQuizSolutionFormatter
{
    public static string Format(
        ProportionQuizContract contract,
        AppLanguage language,
        CultureInfo culture,
        string? answerUnit = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(culture);

        string unit = answerUnit ?? contract.AnswerUnit;
        string a = contract.A.ToString("N0", culture);
        string b = contract.B.ToString("N0", culture);
        string c = contract.C.ToString("N0", culture);
        string answer = contract.CorrectAnswer.ToString("N0", culture);
        string calculation = contract.IsDirect
            ? $"{b} ÷ {a} × {c}"
            : $"{a} × {b} ÷ {c}";

        if (contract.AsksForAdditionalPeople)
        {
            calculation += $" − {a}";
        }

        string answerLabel = QuizContentCatalog.Text(language, "ProportionQuizSolutionFormatter.Format.001");
        return $"{GetSolutionLead(contract, language, unit)}{Environment.NewLine}" +
               $"{calculation} = {answer} {unit}{Environment.NewLine}" +
               $"{answerLabel}: {answer} {unit}";
    }

    private static string GetSolutionLead(
        ProportionQuizContract contract,
        AppLanguage language,
        string unit)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return contract.Scenario switch
            {
                ProportionScenarioKind.Clothing =>
                    $"Số {unit} cần dùng là:",
                ProportionScenarioKind.StudentsPlanting =>
                    $"Số {unit} trồng được là:",
                ProportionScenarioKind.Shopping =>
                    $"Số tiền phải trả ({unit}) là:",
                ProportionScenarioKind.VehiclesCargo =>
                    $"Số {unit} chở được là:",
                ProportionScenarioKind.VehiclesFuel =>
                    $"Số {unit} tiêu thụ là:",
                ProportionScenarioKind.ContainersLiquid =>
                    $"Số {unit} chứa được là:",
                ProportionScenarioKind.DistanceTime =>
                    $"Quãng đường ô tô đi được ({unit}) là:",
                ProportionScenarioKind.PaintArea =>
                    $"Diện tích tường sơn được ({unit}) là:",
                ProportionScenarioKind.ProductionItems =>
                    $"Số {unit} làm được là:",
                ProportionScenarioKind.RiceBagsWeight or
                ProportionScenarioKind.FoodWeightGrams or
                ProportionScenarioKind.EggWeightGrams =>
                    $"Khối lượng {contract.SubjectName} ({unit}) là:",
                ProportionScenarioKind.FoodAdditionalPeople =>
                    "Số người đến thêm là:",
                ProportionScenarioKind.WorkersDays or
                ProportionScenarioKind.WorkersJob or
                ProportionScenarioKind.MachinesHours =>
                    $"Số {unit} cần để hoàn thành công việc là:",
                ProportionScenarioKind.FoodPeopleDays =>
                    $"Số {unit} thực phẩm đủ dùng là:",
                ProportionScenarioKind.SalesStock =>
                    $"Số {unit} hàng đủ bán là:",
                ProportionScenarioKind.WorkersRequired or
                ProportionScenarioKind.MachinesRequired =>
                    $"Số {unit} cần để hoàn thành công việc đúng thời hạn là:",
                ProportionScenarioKind.TapsTime =>
                    $"Thời gian để bơm đầy bể ({unit}) là:",
                ProportionScenarioKind.TravelSpeedTime =>
                    $"Thời gian đi hết quãng đường ({unit}) là:",
                ProportionScenarioKind.TransportTrips =>
                    $"Số {unit} mỗi xe cần chở là:",
                ProportionScenarioKind.PackagingCount =>
                    $"Số {unit} {contract.SubjectName} sau khi đóng lại là:",
                _ => $"Số {unit} cần tìm là:"
            };
        }

        return contract.Scenario switch
        {
            ProportionScenarioKind.Clothing =>
                $"The fabric needed ({unit}) is:",
            ProportionScenarioKind.StudentsPlanting =>
                $"The number of {unit} planted is:",
            ProportionScenarioKind.Shopping =>
                $"The total cost in {unit} is:",
            ProportionScenarioKind.VehiclesCargo =>
                $"The rice carried ({unit}) is:",
            ProportionScenarioKind.VehiclesFuel =>
                $"The fuel used ({unit}) is:",
            ProportionScenarioKind.ContainersLiquid =>
                $"The liquid held ({unit}) is:",
            ProportionScenarioKind.DistanceTime =>
                $"The distance traveled in {unit} is:",
            ProportionScenarioKind.PaintArea =>
                $"The wall area covered in {unit} is:",
            ProportionScenarioKind.ProductionItems =>
                $"The number of {unit} made is:",
            ProportionScenarioKind.RiceBagsWeight or
            ProportionScenarioKind.FoodWeightGrams or
            ProportionScenarioKind.EggWeightGrams =>
                $"The weight of the {contract.SubjectName} in {unit} is:",
            ProportionScenarioKind.FoodAdditionalPeople =>
                "The number of additional people is:",
            ProportionScenarioKind.WorkersDays or
            ProportionScenarioKind.WorkersJob or
            ProportionScenarioKind.MachinesHours =>
                $"The time needed to finish the job ({unit}) is:",
            ProportionScenarioKind.FoodPeopleDays =>
                $"The time the food will last ({unit}) is:",
            ProportionScenarioKind.SalesStock =>
                $"The time the stock will last ({unit}) is:",
            ProportionScenarioKind.WorkersRequired or
            ProportionScenarioKind.MachinesRequired =>
                $"The number of {unit} needed to finish the job on time is:",
            ProportionScenarioKind.TapsTime =>
                $"The time needed to fill the tank ({unit}) is:",
            ProportionScenarioKind.TravelSpeedTime =>
                $"The time needed to travel the route ({unit}) is:",
            ProportionScenarioKind.TransportTrips =>
                $"The number of {unit} needed by each truck is:",
            ProportionScenarioKind.PackagingCount =>
                $"The number of {unit} of {contract.SubjectName} after repacking is:",
            _ => $"The required number of {unit} is:"
        };
    }
}
