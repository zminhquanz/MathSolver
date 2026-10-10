using MathSolver.Models;
using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private bool _updatingLearning;
    private QuestionKnowledgeGroup _learningGroup = QuestionKnowledgeGroup.Objects;
    private QuestionKnowledgeGroup[] _learningGroups = [];
    private ArithmeticOperation[] _learningOperations = Enum.GetValues<ArithmeticOperation>();
    private BankQuestionFamily _family;
    private int _storyVariant;
    private int[] _storyVariants = [];
    private FindXUnknownRole _unknownRole;
    private FindXUnknownRole[] _unknownRoles = [];
    private QuestionLearningProfile CurrentLearningProfile => new(_learningGroup);
    private ArithmeticOperation SelectedLearningOperation => OperationPicker.SelectedIndex >= 0
        && OperationPicker.SelectedIndex < _learningOperations.Length ? _learningOperations[OperationPicker.SelectedIndex] : ArithmeticOperation.Add;
    private void RefreshLearningPickers(ArithmeticOperation operation)
    {
        _updatingLearning = true;
        try
        {
            LearningGroupPanel.IsVisible = !ReasoningStoryCatalogue.Supports(_family);
            if (ReasoningStoryCatalogue.Supports(_family))
            {
                string[] keys = _family switch
                {
                    BankQuestionFamily.TwoNumbers => ["Quiz.Elementary.SumDifference", "Quiz.Elementary.SumRatio", "Quiz.Elementary.DifferenceRatio"],
                    BankQuestionFamily.Average => ["Quiz.AverageDirect", "Quiz.AverageTotalToAverage", "Quiz.AverageAverageToTotal", "Quiz.AverageMissingValue", "Quiz.AverageIndirectData", "Quiz.AverageTwoGroups"],
                    BankQuestionFamily.MultiStep => ElementaryQuizGenerator.Types(QuizProblemKind.MultiStep)
                        .Select(type => "Quiz.Elementary." + type).ToArray(),
                    BankQuestionFamily.Motion => ["Quiz.MotionBasic", "Quiz.MotionChasing", "Quiz.MotionMeeting", "Quiz.MotionRiver"],
                    BankQuestionFamily.Proportion => ["Quiz.ProportionDirect", "Quiz.ProportionInverse"],
                    BankQuestionFamily.Decimal => ElementaryQuizGenerator.DecimalStoryTypes.Select(type => "Quiz.Elementary." + type).ToArray(),
                    BankQuestionFamily.Measurement => ElementaryQuizGenerator.MeasurementBankTypes.Select(type => "Quiz.Elementary." + type).ToArray(),
                    BankQuestionFamily.Remainder => ElementaryQuizGenerator.RemainderStoryTypes.Select(type => "Quiz.Elementary." + type).ToArray(),
                    BankQuestionFamily.Time => ElementaryQuizGenerator.TimeStoryTypes.Select(type => "Quiz.Elementary." + type).ToArray(),
                    BankQuestionFamily.FractionQuantity => ElementaryQuizGenerator.FractionQuantityStoryTypes.Select(type => "Quiz.Elementary." + type).ToArray(),
                    BankQuestionFamily.Geometry => ReasoningStoryCatalogue.Variants(_family, (CurriculumTier)(Math.Max(0, StarsPicker.SelectedIndex) + 1))
                        .Select(variant => "Quiz.Geometry" + ReasoningStoryCatalogue.GeometryProfile(variant).Shape).ToArray(),
                    BankQuestionFamily.Data => ElementaryQuizGenerator.DataChartTypes.Select(type => "Quiz.Elementary." + type).ToArray(),
                    BankQuestionFamily.Probability => ElementaryQuizGenerator.Types(QuizProblemKind.Probability).Select(type => "Quiz.Elementary." + type).ToArray(),
                    _ => ["Quiz.PercentageRatio", "Quiz.PercentageValue", "Quiz.PercentageWhole"]
                };
                _storyVariants = ReasoningStoryCatalogue.Variants(_family, (CurriculumTier)(Math.Max(0, StarsPicker.SelectedIndex) + 1));
                if (!_storyVariants.Contains(_storyVariant)) _storyVariant = _storyVariants[0];
                OperationPicker.ItemsSource = keys.Select(LocalizationService.TranslateKey).ToArray();
                if (_family == BankQuestionFamily.Geometry)
                    OperationPicker.ItemsSource = _storyVariants.Select(variant => {
                        var profile = ReasoningStoryCatalogue.GeometryProfile(variant);
                        return LocalizationService.TranslateKey("Quiz.Geometry" + profile.Shape) + " · "
                            + GeometryReasoningText.MeasurementName(profile.Measurement, AppLanguageManager.CurrentLanguage);
                    }).ToArray();
                IllustratedQuizPicker.SetKeys(OperationPicker, keys);
                OperationPicker.SelectedIndex = Array.IndexOf(_storyVariants, _storyVariant);
                return;
            }
            string L(string key) => LocalizationService.TranslateKey("Learning." + key);
            _learningGroups = QuestionLearningProfile.Groups();
            if (_learningGroup is QuestionKnowledgeGroup.Mass or QuestionKnowledgeGroup.Length or QuestionKnowledgeGroup.Transport)
                _learningGroup = QuestionKnowledgeGroup.Measurement;
            if (!_learningGroups.Contains(_learningGroup)) _learningGroup = QuestionKnowledgeGroup.Objects;
            LearningGroupPicker.ItemsSource = _learningGroups.Select(g => L("Group." + g)).ToArray();
            IllustratedQuizPicker.SetKeys(LearningGroupPicker, _learningGroups.Select(g => "Learning.Group." + g));
            LearningGroupPicker.SelectedIndex = Array.IndexOf(_learningGroups, _learningGroup);
            _learningOperations = Enum.GetValues<ArithmeticOperation>();
            OperationPicker.ItemsSource = _learningOperations.Select(op => T(op.ToString())).ToArray();
            IllustratedQuizPicker.SetKeys(OperationPicker, _learningOperations.Select(op => "AiBank." + op));
            OperationPicker.SelectedIndex = Math.Max(0, Array.IndexOf(_learningOperations, operation));
        }
        finally { _updatingLearning = false; }
    }
    private void OnLearningGroupChanged(object? sender, EventArgs e)
    {
        if (_updatingLearning || LearningGroupPicker.SelectedIndex < 0) return;
        _learningGroup = _learningGroups[LearningGroupPicker.SelectedIndex];
        RefreshFindXRoles();
    }

    private void OnBankProblemChanged(object? sender, EventArgs e)
    {
        if (_updating || ProblemPicker.SelectedIndex < 0) return;
        _family = Enum.IsDefined((BankQuestionFamily)ProblemPicker.SelectedIndex)
            ? (BankQuestionFamily)ProblemPicker.SelectedIndex : BankQuestionFamily.Arithmetic;
        RefreshLearningPickers(SelectedLearningOperation);
        RefreshFindXRoles();
    }

    private void OnBankOperationChanged(object? sender, EventArgs e)
    {
        if (_updating || _updatingLearning) return;
        if (ReasoningStoryCatalogue.Supports(_family))
        {
            if (sender == OperationPicker && OperationPicker.SelectedIndex >= 0)
                _storyVariant = _storyVariants[OperationPicker.SelectedIndex];
            RefreshLearningPickers(ArithmeticOperation.Add);
        }
        RefreshFindXRoles();
    }

    private void RefreshFindXRoles()
    {
        FindXRolePanel.IsVisible = _family == BankQuestionFamily.FindX;
        if (!FindXRolePanel.IsVisible) { _unknownRole = FindXUnknownRole.None; return; }
        _updatingLearning = true;
        try
        {
            _unknownRoles = FindXQuestionCatalogue.Available(CurrentLearningProfile, SelectedLearningOperation,
                (CurriculumTier)(Math.Max(0, StarsPicker.SelectedIndex) + 1)).Select(s => s.Role).Distinct().ToArray();
            if (!_unknownRoles.Contains(_unknownRole)) _unknownRole = _unknownRoles.FirstOrDefault();
            FindXRolePicker.ItemsSource = _unknownRoles.Select(r => LocalizationService.TranslateKey("FindXBank.Role." + r)).ToArray();
            IllustratedQuizPicker.SetKeys(FindXRolePicker, _unknownRoles.Select(role => "FindXBank.Role." + role));
            FindXRolePicker.SelectedIndex = Array.IndexOf(_unknownRoles, _unknownRole);
            string[] keys = ["Quiz.FindXSum", "Quiz.FindXDifference", "Quiz.FindXProduct", "Quiz.FindXQuotient"];
            int operation = OperationPicker.SelectedIndex;
            OperationPicker.ItemsSource = keys.Select(LocalizationService.TranslateKey).ToArray();
            IllustratedQuizPicker.SetKeys(OperationPicker, keys);
            OperationPicker.SelectedIndex = Math.Max(0, operation);
        }
        finally { _updatingLearning = false; }
    }

    private void OnFindXRoleChanged(object? sender, EventArgs e)
    {
        if (!_updatingLearning && FindXRolePicker.SelectedIndex >= 0 && FindXRolePicker.SelectedIndex < _unknownRoles.Length)
            _unknownRole = _unknownRoles[FindXRolePicker.SelectedIndex];
    }
}
