using MathSolver.Views;
using MathSolver.Controls;

namespace MathSolver.Services;

/// <summary>
/// Reveals calculator output after an explicit Calculate button click.
/// Content growth, disclosure buttons and programmatic display refreshes do
/// not request scrolling. No layout subscriptions remain between calculations.
/// </summary>
internal sealed class CalculationAutoScroll : IDisposable
{
    private readonly Button _button;
    private readonly ScrollView _scrollView;
    private readonly CalculationPage _page;
    private readonly VisualElement[] _outputs;
    private bool _cancelled;
    private int _requestVersion;

    private CalculationAutoScroll(
        Button button, ScrollView scrollView, CalculationPage page,
        VisualElement[] outputs)
    {
        _button = button;
        _scrollView = scrollView;
        _page = page;
        _outputs = outputs;
        _page.Disappearing += OnDisappearing;
        _button.Unloaded += OnUnloaded;
    }

    public static CalculationAutoScroll? Begin(
        object? sender, params VisualElement[] outputs)
    {
        // RefreshNumberDisplay calls the same handlers with the view as sender.
        if (sender is not Button button)
            return null;

        ScrollView? scrollView = null;
        for (Element? element = button.Parent; element is not null; element = element.Parent)
        {
            if (element is ScrollView candidate &&
                candidate.Orientation != ScrollOrientation.Horizontal)
                scrollView ??= candidate;

            if (element is CalculationPage page && scrollView is not null)
                return new(button, scrollView, page, outputs);
        }

        return null;
    }

    public void Reveal(params VisualElement[] outputs)
    {
        int version = ++_requestVersion;
        _scrollView.Dispatcher.Dispatch(async () =>
        {
            // The shared workspace waits for its own fresh arrangement. Never
            // reject a new result using the preceding hidden-result extent.
            if (SolverWorkspace.GetIsEnabled(_scrollView))
            {
                if (!_cancelled && version == _requestVersion && _scrollView.Handler is not null
                    && Shell.Current?.CurrentPage == _page && IsVisibleInPage(_button)
                    && outputs.FirstOrDefault(IsVisibleInPage) is { } result)
                    SolverWorkspace.RevealResult(result);
                return;
            }
            // Preserve native settlement for legacy complex graph/power pages.
            await Task.Delay(64);
            if (_cancelled || version != _requestVersion ||
                _scrollView.Handler is null ||
                Shell.Current?.CurrentPage != _page ||
                !IsVisibleInPage(_button) ||
                !outputs.Any(IsVisibleInPage) ||
                _scrollView.Content is not { Height: > 0 } content ||
                _scrollView.Height <= 0 || content.Height <= _scrollView.Height)
                return;

            var output = outputs.FirstOrDefault(IsVisibleInPage);
            if (output is null) return;
            // Complex graph/power pages keep their existing workspace, but
            // should reveal the beginning of output rather than the page end.
            double top = 0;
            Element? element = output;
            while (element is VisualElement visual && !ReferenceEquals(element, content))
            { top += visual.Y; element = visual.Parent; }
            if (!ReferenceEquals(element, content) || !double.IsFinite(top)) return;
            double leading = Math.Min(80 * ResponsiveLayoutPolicy.TextScale, output.Height);
            if (top >= _scrollView.ScrollY && top + leading <= _scrollView.ScrollY + _scrollView.Height) return;
            try { await _scrollView.ScrollToAsync(0, Math.Max(0, top), animated: false); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) when (_scrollView.Handler is null || _page.Window is null
                || (_page.Window is { } window && WindowUiLifetime.IsClosing(window))) { }
        });
    }

    private bool IsVisibleInPage(VisualElement view)
    {
        for (Element? element = view; element is not null; element = element.Parent)
        {
            if (element is VisualElement visual && !visual.IsVisible)
                return false;
            if (ReferenceEquals(element, _page))
                return true;
        }
        return false;
    }

    private void OnDisappearing(object? sender, EventArgs e) => _cancelled = true;
    private void OnUnloaded(object? sender, EventArgs e) => _cancelled = true;

    public void Dispose()
    {
        _page.Disappearing -= OnDisappearing;
        _button.Unloaded -= OnUnloaded;
        Reveal(_outputs);
    }
}
