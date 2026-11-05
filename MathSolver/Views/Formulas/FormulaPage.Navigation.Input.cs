namespace MathSolver.Views;

public partial class FormulaPage
{
    private bool HandleFormulaChooserKey(int key, bool reverse) => _topicChooser.HandleKey(key, reverse);
    private static void FocusFormulaChooserControl(VisualElement view) => MathSolver.Controls.Navigation.TopicChooser.FocusControl(view);
    private bool IsFormulaChooserControlFocused(VisualElement view) => MathSolver.Controls.Navigation.TopicChooser.IsControlFocused(view);
    private void AttachFormulaChooserKeyboard() => _topicChooser.AttachKeyboard();
    private void DetachFormulaChooserKeyboard() => _topicChooser.DetachKeyboard();
}
