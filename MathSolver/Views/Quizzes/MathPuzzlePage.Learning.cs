using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private bool _updatingLearning;
    private QuestionKnowledgeGroup _learningGroup = QuestionKnowledgeGroup.Objects;
    private QuestionKnowledgeGroup[] _learningGroups = [];
    private QuestionLearningProfile CurrentLearningProfile => new(_learningGroup);
    private void RefreshLearningPickers()
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
        }
        finally { _updatingLearning = false; }
#if ANDROID
        AndroidPickerVisualHelper.Attach(LearningGroupPicker);
#endif
    }
    private void OnLearningGroupChanged(object? sender, EventArgs e)
    {
        if (_updatingLearning || LearningGroupPicker.SelectedIndex < 0) return;
        _learningGroup = _learningGroups[LearningGroupPicker.SelectedIndex];
        RegenerateLearningQuestion();
    }
    private void RegenerateLearningQuestion()
    {
        UpdateProblemOperationPanel();
        ResetQuizSessionState();
        GenerateAlgorithmQuestion();
    }
}
