using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MathSolver.Services;

/// <summary>
/// Global UX helper for pages whose result/feedback Border is appended below
/// the current viewport. After the first layout has settled, any meaningful
/// vertical content growth automatically scrolls the owning ScrollView to the
/// bottom. Horizontal-only ScrollViews are ignored.
///
/// This is intentionally layout-driven instead of being tied to individual
/// Calculate buttons, so the same behavior also covers validation cards,
/// solution cards, AI diagnostics and future result Borders throughout the app.
/// </summary>
public static class AutoScrollOnContentGrowthBehavior
{
    public static readonly BindableProperty IsEnabledProperty =
        BindableProperty.CreateAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoScrollOnContentGrowthBehavior),
            false,
            propertyChanged: OnIsEnabledChanged);

    private static readonly ConditionalWeakTable<ScrollView, State> States =
        new();

    private static readonly BindableProperty ContentStateProperty =
        BindableProperty.CreateAttached(
            "ContentState",
            typeof(State),
            typeof(AutoScrollOnContentGrowthBehavior),
            default(State));

    public static bool GetIsEnabled(BindableObject bindable) =>
        (bool)bindable.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(BindableObject bindable, bool value) =>
        bindable.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        if (bindable is not ScrollView scrollView)
        {
            return;
        }

        bool enabled = newValue is true;

        if (enabled)
        {
            State state = States.GetOrCreateValue(scrollView);
            if (state.IsAttached)
            {
                return;
            }

            state.Owner = scrollView;
            state.IsAttached = true;
            scrollView.Loaded += OnLoaded;
            scrollView.Unloaded += OnUnloaded;
            scrollView.PropertyChanged += OnScrollViewPropertyChanged;
            AttachContent(scrollView, state);
            _ = ArmAfterInitialLayoutAsync(scrollView, state);
            return;
        }

        if (!States.TryGetValue(scrollView, out State? existing))
        {
            return;
        }

        existing.IsAttached = false;
        existing.IsArmed = false;
        existing.Generation++;
        scrollView.Loaded -= OnLoaded;
        scrollView.Unloaded -= OnUnloaded;
        scrollView.PropertyChanged -= OnScrollViewPropertyChanged;
        DetachContent(existing);
        States.Remove(scrollView);
    }

    private static void OnLoaded(object? sender, EventArgs e)
    {
        if (sender is ScrollView scrollView &&
            States.TryGetValue(scrollView, out State? state))
        {
            _ = ArmAfterInitialLayoutAsync(scrollView, state);
        }
    }

    private static void OnUnloaded(object? sender, EventArgs e)
    {
        if (sender is not ScrollView scrollView ||
            !States.TryGetValue(scrollView, out State? state))
        {
            return;
        }

        state.IsArmed = false;
        state.Generation++;
        DetachContent(state);
    }

    private static void OnScrollViewPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (sender is not ScrollView scrollView ||
            e.PropertyName != nameof(ScrollView.Content) ||
            !States.TryGetValue(scrollView, out State? state))
        {
            return;
        }

        AttachContent(scrollView, state);

        if (state.IsArmed && state.Content is not null)
        {
            state.LastContentHeight = state.Content.Height;
        }
    }

    private static async Task ArmAfterInitialLayoutAsync(
        ScrollView scrollView,
        State state)
    {
        int generation = ++state.Generation;
        state.IsArmed = false;

        // Do not interpret the page's initial measure passes as a result card
        // appearing. Waiting briefly is important on WinUI where MAUI can
        // report several growing DesiredSize values during first layout.
        await Task.Delay(140).ConfigureAwait(false);

        scrollView.Dispatcher.Dispatch(() =>
        {
            if (!state.IsAttached ||
                generation != state.Generation)
            {
                return;
            }

            AttachContent(scrollView, state);
            state.LastContentHeight =
                state.Content?.Height > 0
                    ? state.Content.Height
                    : 0d;
            state.IsArmed = true;
        });
    }

    private static void AttachContent(
        ScrollView scrollView,
        State state)
    {
        View? content = scrollView.Content;
        if (ReferenceEquals(state.Content, content))
        {
            return;
        }

        DetachContent(state);
        state.Content = content;

        if (content is null)
        {
            return;
        }

        content.SetValue(ContentStateProperty, state);
        content.SizeChanged += OnContentSizeChanged;
        state.LastContentHeight = content.Height;
    }

    private static void DetachContent(State state)
    {
        if (state.Content is not null)
        {
            state.Content.SizeChanged -= OnContentSizeChanged;
            state.Content.ClearValue(ContentStateProperty);
        }

        state.Content = null;
        state.LastContentHeight = 0d;
        state.ScrollPending = false;
    }

    private static void OnContentSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not View content ||
            content.GetValue(ContentStateProperty) is not State state ||
            state.Owner is not ScrollView scrollView ||
            !ReferenceEquals(state.Content, content))
        {
            return;
        }

        double newHeight = content.Height;
        double previousHeight = state.LastContentHeight;
        state.LastContentHeight = newHeight;

        if (!state.IsAttached ||
            !state.IsArmed ||
            newHeight <= 0d ||
            newHeight - previousHeight < 8d ||
            scrollView.Orientation == ScrollOrientation.Horizontal ||
            scrollView.Height <= 0d ||
            newHeight <= scrollView.Height + 1d)
        {
            return;
        }

        ScheduleScrollToBottom(scrollView, state);
    }

    private static void ScheduleScrollToBottom(
        ScrollView scrollView,
        State state)
    {
        if (state.ScrollPending)
        {
            return;
        }

        state.ScrollPending = true;
        int generation = state.Generation;

        scrollView.Dispatcher.Dispatch(async () =>
        {
            try
            {
                // Let IsVisible/Border measurement finish before asking the
                // platform ScrollViewer to move. This avoids the old WinUI
                // behavior where ScrollToAsync used the previous extent.
                await Task.Delay(32);

                if (!state.IsAttached ||
                    !state.IsArmed ||
                    generation != state.Generation ||
                    state.Content is null)
                {
                    return;
                }

                await scrollView.ScrollToAsync(
                    0,
                    Math.Max(0d, state.Content.Height),
                    animated: false);
            }
            finally
            {
                state.ScrollPending = false;
            }
        });
    }

    private sealed class State
    {
        public bool IsAttached { get; set; }
        public bool IsArmed { get; set; }
        public bool ScrollPending { get; set; }
        public int Generation { get; set; }
        public double LastContentHeight { get; set; }
        public ScrollView? Owner { get; set; }
        public View? Content { get; set; }
    }
}
