namespace MathSolver.Views;

public partial class CalculationPage
{
    private bool HandleCalculationChooserKey(int key, bool reverse) => _topicChooser.HandleKey(key, reverse);
    private static void FocusCalculationChooserControl(VisualElement view) => MathSolver.Controls.Navigation.TopicChooser.FocusControl(view);
    private bool IsCalculationChooserControlFocused(VisualElement view) => MathSolver.Controls.Navigation.TopicChooser.IsControlFocused(view);
    private void AttachCalculationChooserKeyboard() => _topicChooser.AttachKeyboard();
    private void DetachCalculationChooserKeyboard() => _topicChooser.DetachKeyboard();
}
