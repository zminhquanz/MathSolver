using MathSolver.Services;

namespace MathSolver.Views;

public partial class DataManagementPage
{
    private bool _sqlGridActionsLayoutQueued;
    private int _sqlGridActionColumns;
    private View[] _sqlGridVisibleActions = [];

    private void OnSqlGridActionsSizeChanged(object? sender, EventArgs e) => QueueSqlGridActionsLayout();

    private void QueueSqlGridActionsLayout()
    {
        if (!_appeared || _sqlGridActionsLayoutQueued) return;
        _sqlGridActionsLayoutQueued = true;
        // Visibility can change during a native layout pass (edit/insert/cancel).
        // Reflow the existing buttons on the next UI turn, without recreating them.
        if (!Dispatcher.Dispatch(() =>
        {
            _sqlGridActionsLayoutQueued = false;
            if (!_appeared || _closing || SqlGridActions.Width <= 0 || SqlGridActions.Handler?.MauiContext is null
                || Window is not { } window || WindowUiLifetime.IsClosing(window)) return;
            var actions = SqlGridActions.Children.OfType<View>().Where(view => view.IsVisible).ToArray();
            int columns = Math.Min(Math.Max(1, actions.Length), ResponsiveLayoutPolicy.Columns(
                SqlGridActions.Width, 140, 3, SqlGridActions.ColumnSpacing));
            if (columns == _sqlGridActionColumns && actions.SequenceEqual(_sqlGridVisibleActions)) return;
            _sqlGridActionColumns = columns;
            _sqlGridVisibleActions = actions;
            SqlGridActions.ColumnDefinitions.Clear();
            SqlGridActions.RowDefinitions.Clear();
            for (int i = 0; i < columns; i++) SqlGridActions.ColumnDefinitions.Add(new(GridLength.Star));
            for (int i = 0; i < Math.Max(1, (actions.Length + columns - 1) / columns); i++)
                SqlGridActions.RowDefinitions.Add(new(GridLength.Auto));
            foreach (var child in SqlGridActions.Children.OfType<View>())
            {
                SqlGridActions.SetRow(child, 0);
                SqlGridActions.SetColumn(child, 0);
                SqlGridActions.SetColumnSpan(child, 1);
            }
            for (int i = 0; i < actions.Length; i++)
            {
                SqlGridActions.SetRow(actions[i], i / columns);
                SqlGridActions.SetColumn(actions[i], i % columns);
                // An incomplete last row uses its remaining space (e.g. Reload
                // below Add/Edit on a phone), rather than leaving a narrow orphan.
                SqlGridActions.SetColumnSpan(actions[i], i == actions.Length - 1 ? columns - i % columns : 1);
            }
        })) _sqlGridActionsLayoutQueued = false;
    }
}
