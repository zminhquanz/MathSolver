using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private readonly Label _phoneTitle = new()
    {
        FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center,
        MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation, Margin = new Thickness(12, 4), IsVisible = false
    };
    private Border _setupCard = null!;
    private Grid _phoneMoreActions = null!;
    private EmojiActionButton _phoneSetup = null!;
    private bool _phoneEntryWorkspace;

    private void BuildPhoneWorkspace(Grid root, Border setup, Grid heading)
    {
        _setupCard = setup;
        _phoneTitle.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        root.Add(_phoneTitle);
        _phoneSetup = (EmojiActionButton)MirrorAction("Workspace.EditSetup", "⚙", _setupToggle);
        _phoneSetup.AutomationId = "MatrixPhoneSetup";
        _phoneSetup.IsVisible = false;
        heading.ColumnDefinitions.Add(new(GridLength.Auto));
        heading.Add(_phoneSetup, 4);
        _phoneMoreActions = FlowGrid(120, 2);
        _phoneMoreActions.IsVisible = false;
        _phoneMoreActions.Add(MirrorAction("Workspace.Copy", "📋", _copySelection));
        _phoneMoreActions.Add(MirrorAction("Workspace.Paste", "", _pasteCells));
        _phoneMoreActions.Add(MirrorAction("Workspace.Clear", "", _clearCells));
        _phoneMoreActions.Add(MirrorAction("TechnicalDetails", "ⓘ", _technicalDetailsToggle));
        _moreTools.Children.Insert(0, _phoneMoreActions);
    }

    private Button MirrorAction(string key, string icon, Button original)
    {
        var action = ActionButton(key, icon, (_, _) =>
        {
            // A phone inspector disables pointer input to the table. Close it
            // before invoking tools that finish an edit or change the selection.
            TryCloseMatrixDetails();
            ((IButtonController)original).SendClicked();
        }, lockDuringWork: false);
        action.SetBinding(IsEnabledProperty, new Binding(nameof(Button.IsEnabled), source: original));
        return action;
    }

    private void RefreshPhoneWorkspace(bool phone)
    {
        if (_phoneEntryWorkspace != phone)
        {
            _phoneEntryWorkspace = phone;
            if (phone)
            {
                _matrixMain.RemoveDynamicResource(Grid.PaddingProperty);
                _matrixMain.Padding = new Thickness(8, 4);
            }
            else _matrixMain.SetDynamicResource(Grid.PaddingProperty, "Density.ScrollPadding");
            _viewport.FitSmallCells = phone;
            _viewport.Refresh();
        }
        _phoneTitle.IsVisible = phone;
        _phoneTitle.Text = T("Title") + "  ·  " + _heroSymbol.Text;
        _setupCard.IsVisible = !phone;
        _phoneSetup.IsVisible = phone;
        SetMatrixToolPresentation(_phoneSetup, "Workspace.EditSetup", "⚙");
        _technicalDetailsToggle.IsVisible = !phone;
        _phoneMoreActions.IsVisible = phone;
        _clipboardTools.IsVisible = !phone;
        _cellInfo.IsVisible = !phone || !_tableExpanded;
        if (phone) _inputStatus.IsVisible = _range.IsVisible = false;
        foreach (EmojiActionButton tab in _matrices) tab.Emoji = phone ? "" : "▦";
        // On phones the footer has one row; clipboard tools remain in More.
        _copySelection.IsVisible = HasInputs && !phone;
        _footerStatusRow.IsVisible = (_message.IsVisible || _statusDetailsButton.IsVisible)
            && (!phone || _errorKey is not null);
    }
}
