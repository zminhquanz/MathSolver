using MathSolver.Models;
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
            string L(string key) => LocalizationService.TranslateKey("Learning." + key);
            _learningGroups = QuestionLearningProfile.Groups();
            if (_learningGroup is QuestionKnowledgeGroup.Mass or QuestionKnowledgeGroup.Length or QuestionKnowledgeGroup.Transport)
                _learningGroup = QuestionKnowledgeGroup.Measurement;
            if (!_learningGroups.Contains(_learningGroup)) _learningGroup = QuestionKnowledgeGroup.Objects;
            LearningGroupPicker.ItemsSource = _learningGroups.Select(g => L("Group." + g)).ToArray();
            LearningGroupPicker.SelectedIndex = Array.IndexOf(_learningGroups, _learningGroup);
            _learningOperations = Enum.GetValues<ArithmeticOperation>();
            OperationPicker.ItemsSource = _learningOperations.Select(op => T(op.ToString())).ToArray();
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
        if (!_updating && !_updatingLearning) RefreshFindXRoles();
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
            FindXRolePicker.SelectedIndex = Array.IndexOf(_unknownRoles, _unknownRole);
            string[] keys = ["Quiz.FindXSum", "Quiz.FindXDifference", "Quiz.FindXProduct", "Quiz.FindXQuotient"];
            int operation = OperationPicker.SelectedIndex;
            OperationPicker.ItemsSource = keys.Select(LocalizationService.TranslateKey).ToArray();
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
