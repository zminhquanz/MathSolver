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
                _storyVariants = AiCreativeAuthoringChoices.Variants(_family,
                    (CurriculumTier)(Math.Max(0, StarsPicker.SelectedIndex) + 1));
                if (_storyVariants.Length == 0)
                    throw new InvalidOperationException($"No AI story variants for {_family} at the selected difficulty.");
                if (!_storyVariants.Contains(_storyVariant)) _storyVariant = _storyVariants[0];
                string[] keys = _storyVariants.Select(variant =>
                    QuestionAuthoringChoices.VariantKey(_family, variant)).ToArray();
                string[] labels = _family == BankQuestionFamily.Geometry
                    ? _storyVariants.Select(variant => {
                        var profile = ReasoningStoryCatalogue.GeometryProfile(variant);
                        return LocalizationService.TranslateKey("Quiz.Geometry" + profile.Shape) + " · "
                            + GeometryReasoningText.MeasurementName(profile.Measurement, AppLanguageManager.CurrentLanguage);
                    }).ToArray()
                    : keys.Select(LocalizationService.TranslateKey).ToArray();
                SetLearningPicker(OperationPicker, labels, keys, Array.IndexOf(_storyVariants, _storyVariant));
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
            string[] operationKeys = _family == BankQuestionFamily.FindX
                ? ["Quiz.FindXSum", "Quiz.FindXDifference", "Quiz.FindXProduct", "Quiz.FindXQuotient"]
                : _learningOperations.Select(op => "AiBank." + op).ToArray();
            SetLearningPicker(OperationPicker, operationKeys.Select(LocalizationService.TranslateKey).ToArray(), operationKeys,
                Math.Max(0, Array.IndexOf(_learningOperations, operation)));
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
        _family = AiCreativeAuthoringChoices.FamilyAt(ProblemPicker.SelectedIndex);
        RefreshLearningPickers(SelectedLearningOperation);
        RefreshFindXRoles();
    }

    private void OnBankOperationChanged(object? sender, EventArgs e)
    {
        if (_updating || _updatingLearning) return;
        if (ReasoningStoryCatalogue.Supports(_family))
        {
            if (sender == OperationPicker)
            {
                int index = OperationPicker.SelectedIndex;
                if (index < 0 || index >= _storyVariants.Length) return;
                _storyVariant = _storyVariants[index];
                // Do not replace this picker's items from its own selection event.
                // MAUI can defer the reset/index notifications until after the
                // updating guard exits, creating an endless selection loop.
            }
            else if (sender == StarsPicker)
                RefreshLearningPickers(ArithmeticOperation.Add);
        }
        RefreshFindXRoles();
    }

    private void RefreshFindXRoles()
    {
        RefreshMotionHintsAndOptions();
        FindXRolePanel.IsVisible = _family == BankQuestionFamily.FindX;
        if (!FindXRolePanel.IsVisible) { _unknownRole = FindXUnknownRole.None; return; }
        _updatingLearning = true;
        try
        {
            _unknownRoles = FindXQuestionCatalogue.Available(CurrentLearningProfile, SelectedLearningOperation,
                (CurriculumTier)(Math.Max(0, StarsPicker.SelectedIndex) + 1)).Select(s => s.Role).Distinct().ToArray();
            if (!_unknownRoles.Contains(_unknownRole)) _unknownRole = _unknownRoles.FirstOrDefault();
            string[] keys = _unknownRoles.Select(role => "FindXBank.Role." + role).ToArray();
            SetLearningPicker(FindXRolePicker, keys.Select(LocalizationService.TranslateKey).ToArray(), keys,
                Array.IndexOf(_unknownRoles, _unknownRole));
        }
        finally { _updatingLearning = false; }
    }

    private void OnFindXRoleChanged(object? sender, EventArgs e)
    {
        if (!_updatingLearning && FindXRolePicker.SelectedIndex >= 0 && FindXRolePicker.SelectedIndex < _unknownRoles.Length)
            _unknownRole = _unknownRoles[FindXRolePicker.SelectedIndex];
    }

    private static void SetLearningPicker(Picker picker, string[] labels, IEnumerable<string> keys, int selectedIndex)
    {
        if (picker.ItemsSource is not IEnumerable<string> current || !current.SequenceEqual(labels))
            picker.ItemsSource = labels;
        IllustratedQuizPicker.SetKeys(picker, keys);
        if (picker.SelectedIndex != selectedIndex) picker.SelectedIndex = selectedIndex;
    }
}
