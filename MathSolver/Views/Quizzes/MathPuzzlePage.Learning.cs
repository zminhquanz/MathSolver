using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private bool _updatingLearning;
    private bool _arithmeticWordProblems;
    private bool _findXWordProblems;
    private bool _fractionWordProblems;
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
        RefreshPracticeFormatPicker(GetSelectedFixedProblemRequest()?.Kind);
#if ANDROID
        AndroidPickerVisualHelper.Attach(LearningGroupPicker);
        AndroidPickerVisualHelper.Attach(PracticeFormatPicker);
#endif
    }
    private bool UsesWordProblems(QuizProblemKind? kind) => kind switch
    {
        QuizProblemKind.Arithmetic => _arithmeticWordProblems,
        QuizProblemKind.FindX => _findXWordProblems,
        QuizProblemKind.Fraction => _fractionWordProblems,
        _ => false
    };
    private PracticeQuestionFormat GetPracticeFormat(QuizProblemKind kind) =>
        GetSelectedFixedProblemRequest()?.Kind == kind
            ? UsesWordProblems(kind) ? PracticeQuestionFormat.WordProblem : PracticeQuestionFormat.Numeric
            : PracticeQuestionFormat.Mixed;
    private void RefreshPracticeFormatPicker(QuizProblemKind? kind)
    {
        string prefix = kind switch
        {
            QuizProblemKind.Arithmetic => "ArithmeticPractice",
            QuizProblemKind.FindX => "FindXPractice",
            QuizProblemKind.Fraction => "FractionBank",
            _ => ""
        };
        if (prefix.Length == 0) return;
        _updatingLearning = true;
        try
        {
            string title = LocalizationService.TranslateKey(prefix + ".Mode");
            PracticeFormatTitleLabel.Text = title;
            SemanticProperties.SetDescription(PracticeFormatPicker, title);
            PracticeFormatHintLabel.Text = LocalizationService.TranslateKey(prefix + ".Hint");
            PracticeFormatPicker.ItemsSource = new[] { prefix + ".Numeric", "FractionBank.WordProblems" }
                .Select(LocalizationService.TranslateKey).ToArray();
            PracticeFormatPicker.SelectedIndex = UsesWordProblems(kind) ? 1 : 0;
        }
        finally { _updatingLearning = false; }
    }
    private void OnPracticeFormatChanged(object? sender, EventArgs e)
    {
        if (_updatingLearning || PracticeFormatPicker.SelectedIndex < 0) return;
        bool wordProblems = PracticeFormatPicker.SelectedIndex == 1;
        switch (GetSelectedFixedProblemRequest()?.Kind)
        {
            case QuizProblemKind.Arithmetic: _arithmeticWordProblems = wordProblems; break;
            case QuizProblemKind.FindX: _findXWordProblems = wordProblems; break;
            case QuizProblemKind.Fraction: _fractionWordProblems = wordProblems; break;
            default: return;
        }
        RegenerateLearningQuestion();
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
