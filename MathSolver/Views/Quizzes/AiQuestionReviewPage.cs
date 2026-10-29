using System.Collections.ObjectModel;
using System.Globalization;
using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Views;

/// <summary>Human review is explicit. Pending drafts are never usable practice questions.</summary>
public sealed class AiQuestionReviewPage : ContentPage
{
    private readonly QuestionBankStore _store;
    private readonly Action<string, bool>? _onResolved;
    private readonly ObservableCollection<Card> _cards = [];
    private readonly CollectionView _items = new()
    {
        SelectionMode = SelectionMode.None,
        ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 16 },
        ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
        VerticalScrollBarVisibility = ScrollBarVisibility.Always,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
        BackgroundColor = Colors.Transparent,
        AutomationId = "AiReviewList"
    };
    private readonly Label _status = Text(size: 14, bold: true);
    private readonly Label _feedback = Text(size: 14, secondary: true);
    private readonly Label _empty = Text(size: 16);
    private readonly ActivityIndicator _activity = new() { WidthRequest = 24, HeightRequest = 24, IsVisible = false };
    private readonly Button _retry = new();
    private readonly Button _back = new() { AutomationId = "AiReviewBack" };
    private readonly Label _selectionCount = Text(size: 14, bold: true);
    private readonly EmojiActionButton _selectAll = BatchButton("SelectAll", "☑️", "SolverOutlinedActionStyle", "AiReviewSelectAll");
    private readonly EmojiActionButton _clearSelection = BatchButton("ClearSelection", "↩️", "SolverOutlinedActionStyle", "AiReviewClearSelection");
    private readonly EmojiActionButton _approveSelected = BatchButton("ApproveSelected", "✅", "SolverPrimaryActionStyle", "AiReviewApproveSelected");
    private readonly EmojiActionButton _rejectSelected = BatchButton("RejectSelected", "🗑️", "SolverOutlinedActionStyle", "AiReviewRejectSelected");
    private readonly EmojiActionButton _approveAll = BatchButton("ApproveAll", "✅", "SolverPrimaryActionStyle", "AiReviewApproveAll");
    private readonly EmojiActionButton _rejectAll = BatchButton("RejectAll", "🗑️", "SolverOutlinedActionStyle", "AiReviewRejectAll");
    private readonly Label _headerTitle = new()
    {
        FontSize = 20, FontAttributes = FontAttributes.Bold,
        LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1
    };
    private readonly Label _headerSubtitle = new()
    {
        FontSize = 11, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1
    };
    private readonly LiveWallpaperView _wallpaper = new();
    private readonly Command<Card> _approveCommand;
    private readonly Command<Card> _rejectCommand;
    private bool _busy;
    private bool _goingBack;
    private bool _changingSelection;
    private bool _visible;
    private int _headerRefreshGeneration;
    private static string T(string key) => LocalizationService.TranslateKey("AiBank.Review." + key);
    // Selection belongs to the stable item, never to a recycled native cell.
    private sealed class Card(PendingQuestionReview pending, string candidate, string reference,
        string calculation, string heading, Action selectionChanged) : BindableObject
    {
        public PendingQuestionReview Pending { get; } = pending;
        public string Candidate { get; } = candidate;
        public string Reference { get; } = reference;
        public string Calculation { get; } = calculation;
        public string Heading { get; } = heading;
        public string SelectionDescription => string.Format(CultureInfo.CurrentCulture, T("SelectQuestion"), Heading, Candidate);
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value || !CanInteract) return;
                _isSelected = value;
                OnPropertyChanged();
                selectionChanged();
            }
        }
        private bool _canInteract = true;
        public bool CanInteract
        {
            get => _canInteract;
            set
            {
                if (_canInteract == value) return;
                _canInteract = value;
                OnPropertyChanged();
            }
        }
    }

    public AiQuestionReviewPage(QuestionBankStore store, Action<string, bool>? onResolved = null)
    {
        _store = store;
        _onResolved = onResolved;
        // These controls format localized keys themselves as selection changes.
        // Keep legacy source-text translation from replacing their live counts.
        foreach (Element control in new Element[] { _selectionCount, _selectAll, _clearSelection,
            _approveSelected, _rejectSelected, _approveAll, _rejectAll, _feedback })
            LocalizationService.ExcludeSubtreeFromLegacyTracking(control);
        Title = T("Title");
        Shell.SetNavBarIsVisible(this, true);
        Shell.SetTabBarIsVisible(this, false);
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false, IsEnabled = false });
        this.SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");
        _approveCommand = new Command<Card>(async card => await ActAsync(card, true), _ => !_busy);
        _rejectCommand = new Command<Card>(async card => await ActAsync(card, false), _ => !_busy);
        _activity.SetDynamicResource(ActivityIndicator.ColorProperty, "PrimaryColor");
        _status.AutomationId = "AiReviewCount";
        _feedback.IsVisible = false;
        _empty.Text = T("Empty");
        _empty.IsVisible = false;
        _retry.Text = "🔄 " + T("Retry");
        SemanticProperties.SetDescription(_retry, T("Retry"));
        _retry.IsVisible = false;
        _retry.HorizontalOptions = LayoutOptions.Start;
        _retry.SetDynamicResource(StyleProperty, "SolverOutlinedActionStyle");
        _retry.Clicked += async (_, _) => await RefreshAsync();
        _selectAll.Clicked += (_, _) => SelectAll(true);
        _clearSelection.Clicked += (_, _) => SelectAll(false);
        _approveSelected.Clicked += async (_, _) => await ResolveAsync(SelectedIds(), true, true);
        _rejectSelected.Clicked += async (_, _) => await ResolveAsync(SelectedIds(), false, true);
        _approveAll.Clicked += async (_, _) => await ResolveAsync(_cards.Select(card => card.Pending.Id).ToArray(), true, true);
        _rejectAll.Clicked += async (_, _) => await ResolveAsync(_cards.Select(card => card.Pending.Id).ToArray(), false, true);
        _selectionCount.AutomationId = "AiReviewSelectionCount";
        _selectionCount.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        _feedback.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");

        bool android = OperatingSystem.IsAndroid();
        _headerTitle.TranslationY = android ? 1 : 0;
        _headerTitle.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        _headerSubtitle.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        SemanticProperties.SetHeadingLevel(_headerTitle, SemanticHeadingLevel.Level1);
        _back.SetDynamicResource(StyleProperty, "PlatformBackButtonStyle");
        InteractiveButtonAnimation.SetIsScopeEnabled(_back, true);
        InteractiveButtonAnimation.SetIsPlatformBackButton(_back, true);
        InteractiveButtonAnimation.SetIsPressAnimationEnabled(_back, false);
        string backDescription = LocalizationService.TranslateKey("About.Back.Accessibility");
        SemanticProperties.SetDescription(_back, backDescription);
        ToolTipProperties.SetText(_back, backDescription);
        _back.Clicked += OnBackClicked;
        var backIcon = new ThemedSvgPathIcon
        {
            Data = "M313-440h487v-80H313l223-223-56-57-320 320 320 320 56-57-223-223Z",
            WidthRequest = android ? 20 : 18, HeightRequest = android ? 20 : 18,
            TranslationX = android ? -2 : 0,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        backIcon.SetBinding(OpacityProperty, new Binding(nameof(Opacity), source: _back));
        AutomationProperties.SetIsInAccessibleTree(backIcon, false);
        var backHost = new Grid
        {
            WidthRequest = android ? 48 : 40, HeightRequest = android ? 48 : 40,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        backHost.Add(_back);
        backHost.Add(backIcon);
        var navigation = new Grid
        {
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = android ? 2 : 6,
            Padding = android ? new Thickness(0, 0, 8, 0) : new Thickness(2, 0, 10, 0)
        };
        navigation.Add(backHost, 0);
        navigation.Add(new EmojiLabel
        {
            Text = "\U0001F50E", // Magnifying glass: matches the pending-review action.
            FontSize = 24, WidthRequest = 32,
            HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        }, 1);
        var headerText = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
        headerText.Add(_headerTitle);
        headerText.Add(_headerSubtitle);
        navigation.Add(headerText, 2);
        Shell.SetTitleView(this, navigation);
        RefreshHeader();
        var count = new Border
        {
            Padding = new Thickness(12, 6), StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = _status,
            HorizontalOptions = LayoutOptions.Start
        };
        count.SetDynamicResource(BackgroundColorProperty, "PrimarySoftColor");
        _status.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        var hint = Text(size: 14, secondary: true);
        hint.Text = T("Hint");
        var progress = new Grid
        {
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 10
        };
        progress.Add(_activity, 0);
        progress.Add(_feedback, 1);
        var header = new VerticalStackLayout
        {
            Spacing = 12, Margin = new Thickness(0, 16, 0, 16),
            HorizontalOptions = LayoutOptions.Center,
            Children = { count, hint, ActionGrid(_approveAll, _rejectAll), _empty, _retry }
        };
        // Constrain the reading column, never the scrolling viewport. This keeps
        // the native scrollbar (including Windows arrow buttons) at the page edge.
        ConstrainWidth(header);
        _items.Header = header;
        _items.Footer = new BoxView { HeightRequest = 24, InputTransparent = true, Color = Colors.Transparent };
        _items.ItemsSource = _cards;
        _items.ItemTemplate = new DataTemplate(CreateCard);
        // A separate Auto row keeps selected actions reachable while the virtualized
        // list scrolls. Adaptive columns keep long localized captions usable on phones.
        var selectionContent = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { _selectionCount, ActionGrid(_selectAll, _clearSelection, 140),
                ActionGrid(_approveSelected, _rejectSelected), progress }
        };
        var selectionBar = new Border
        {
            Padding = new Thickness(12), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = selectionContent,
            HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 4, 0, 8)
        };
        selectionBar.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        selectionBar.SetDynamicResource(Border.StrokeProperty, "BorderColor");
        ConstrainWidth(selectionBar);
        var root = new Grid { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        root.Add(_wallpaper);
        Grid.SetRowSpan(_wallpaper, 2);
        root.Add(_items);
        root.Add(selectionBar, 0, 1);
        Content = root;
        UpdateCount();
    }

    private View CreateCard()
    {
        var heading = Text(size: 16, bold: true);
        heading.SetBinding(Label.TextProperty, nameof(Card.Heading));
        heading.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
        var selector = new CheckBox
        {
            AutomationId = "AiReviewSelectQuestion", VerticalOptions = LayoutOptions.Start,
            MinimumHeightRequest = 48, MinimumWidthRequest = 48
        };
        selector.SetDynamicResource(CheckBox.ColorProperty, "PrimaryColor");
        selector.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(Card.IsSelected), BindingMode.TwoWay));
        selector.SetBinding(IsEnabledProperty, nameof(Card.CanInteract));
        selector.SetBinding(SemanticProperties.DescriptionProperty, nameof(Card.SelectionDescription));
        var headingRow = new Grid
        {
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8
        };
        headingRow.Add(selector);
        headingRow.Add(heading, 1);
        var comparison = new Grid { ColumnSpacing = 16, RowSpacing = 12 };
        comparison.Add(Section("Candidate", nameof(Card.Candidate), highlighted: true));
        comparison.Add(Section("Reference", nameof(Card.Reference)));
        AdaptiveColumns.SetMinimumColumnWidth(comparison, 360);

        var approve = ActionButton("ApproveAction", "✅", "SolverPrimaryActionStyle", _approveCommand, "AiReviewApprove");
        var reject = ActionButton("Reject", "🗑️", "SolverOutlinedActionStyle", _rejectCommand, "AiReviewReject");
        var actions = new Grid { ColumnSpacing = 12, RowSpacing = 10, HorizontalOptions = LayoutOptions.End };
        actions.Add(approve);
        actions.Add(reject);
        AdaptiveColumns.SetMinimumColumnWidth(actions, 240);
        var content = new VerticalStackLayout { Spacing = 16 };
        content.Add(headingRow);
        content.Add(comparison);
        content.Add(Section("Calculation", nameof(Card.Calculation)));
        content.Add(actions);
        actions.SetBinding(WidthRequestProperty, new Binding(nameof(Width), source: content,
            converter: new ReadingWidthConverter(600, 0)));

        var selectedSurface = new Border
        {
            StrokeThickness = 0, InputTransparent = true,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, Margin = new Thickness(-12)
        };
        selectedSurface.SetDynamicResource(BackgroundColorProperty, "PrimarySoftColor");
        selectedSurface.SetBinding(IsVisibleProperty, nameof(Card.IsSelected));
        AutomationProperties.SetIsInAccessibleTree(selectedSurface, false);
        var card = new Border
        {
            Content = new Grid { Children = { selectedSurface, content } },
            HorizontalOptions = LayoutOptions.Center
        };
        card.SetDynamicResource(StyleProperty, "SolverCardStyle");
        card.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        card.Padding = new Thickness(16);
        ConstrainWidth(card);
        // A full-width item container keeps centered cards independent of the
        // native CollectionView's cell alignment and preserves virtualization.
        return new Grid { Children = { card } };
    }

    private static Border Section(string caption, string binding, bool highlighted = false)
    {
        var label = Text(size: 13, bold: true, secondary: !highlighted);
        label.Text = T(caption);
        SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level3);
        var body = Text(size: 16);
        body.SetBinding(Label.TextProperty, binding);
        var section = new Border
        {
            Padding = new Thickness(14),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new VerticalStackLayout { Spacing = 8, Children = { label, body } }
        };
        section.SetDynamicResource(BackgroundColorProperty, highlighted ? "PrimarySoftColor" : "SurfaceAltColor");
        section.SetDynamicResource(Border.StrokeProperty, highlighted ? "PrimaryBorderColor" : "BorderColor");
        // These panels have opaque surfaces, so use the corresponding text roles.
        label.SetDynamicResource(Label.TextColorProperty, highlighted ? "TextPrimaryColor" : "TextSecondaryColor");
        body.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        return section;
    }

    private static Label Text(double size = 15, bool bold = false, bool secondary = false)
    {
        var label = new Label
        {
            LineBreakMode = LineBreakMode.WordWrap, FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineHeight = 1.2
        };
        label.SetDynamicResource(Label.TextColorProperty, secondary ? "WallpaperTextSecondaryColor" : "WallpaperTextPrimaryColor");
        return label;
    }

    private static Button ActionButton(string key, string icon, string style, Command<Card> command, string automationId)
    {
        var button = BatchButton(key, icon, style, automationId);
        button.Command = command;
        button.SetBinding(Button.CommandParameterProperty, ".");
        return button;
    }

    private static EmojiActionButton BatchButton(string key, string icon, string style, string automationId)
    {
        var button = new EmojiActionButton { Caption = T(key), Emoji = icon, AutomationId = automationId };
        button.SetDynamicResource(StyleProperty, style);
        button.MinimumHeightRequest = 48;
        button.Padding = new Thickness(16, 12);
        DisplayDensityManager.BindControl(button);
        button.FontSize = 14;
        button.LineBreakMode = LineBreakMode.WordWrap;
        return button;
    }

    private static Grid ActionGrid(View first, View second, double minimumColumnWidth = 240)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 8, Children = { first, second } };
        AdaptiveColumns.SetMinimumColumnWidth(grid, minimumColumnWidth);
        return grid;
    }

    private void ConstrainWidth(View view)
    {
        var binding = new Binding(nameof(Width), source: _items, converter: new ReadingWidthConverter(1160, 48));
        view.SetBinding(WidthRequestProperty, binding);
    }

    private sealed class ReadingWidthConverter(double maximum, double inset) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double width = value is double w && double.IsFinite(w) && w > 0 ? w : 320;
            return Math.Max(1, Math.Min(maximum, width - inset));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        Shell.SetTabBarIsVisible(this, false);
        AppThemeManager.ThemeChanged += OnHeaderResourcesChanged;
        LocalizationService.CultureChanged += OnHeaderResourcesChanged;
        OnHeaderResourcesChanged(this, EventArgs.Empty);
        _wallpaper.Resume();
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        ++_headerRefreshGeneration;
        AppThemeManager.ThemeChanged -= OnHeaderResourcesChanged;
        LocalizationService.CultureChanged -= OnHeaderResourcesChanged;
        _wallpaper.Pause();
        base.OnDisappearing();
    }

    private void RefreshHeader()
    {
        Title = _headerTitle.Text = T("Title");
        _headerSubtitle.Text = T("HeaderSubtitle");
        string back = LocalizationService.TranslateKey("About.Back.Accessibility");
        SemanticProperties.SetDescription(_back, back);
        ToolTipProperties.SetText(_back, back);
        // Use committed palette colors after native theme peers reconnect.
        void ApplyColor(Label label, string key)
        {
            if (Application.Current?.Resources.TryGetValue(key, out var value) != true || value is not Color color) return;
            try { label.TextColor = color; }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ObjectDisposedException)
            { System.Diagnostics.Debug.WriteLine($"AI review header theme text deferred: {error}"); }
        }
        ApplyColor(_headerTitle, "TextPrimaryColor");
        ApplyColor(_headerSubtitle, "TextSecondaryColor");
        UpdateSelectionControls();
    }

    private void OnHeaderResourcesChanged(object? sender, EventArgs e)
    {
        int generation = ++_headerRefreshGeneration;
        void Refresh()
        {
            if (_visible && generation == _headerRefreshGeneration) RefreshHeader();
        }
        Dispatcher.Dispatch(Refresh);
#if WINDOWS
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(180), Refresh);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(420), Refresh);
#endif
    }

    protected override bool OnBackButtonPressed()
    {
        _ = GoBackAsync();
        return true;
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await GoBackAsync();

    private async Task GoBackAsync()
    {
        if (_goingBack || _busy) return;
        _goingBack = true;
        _back.IsEnabled = false;
        try
        {
            if (Shell.Current is AppShell appShell) await appShell.CloseSettingsAsync(this);
            else await Navigation.PopAsync();
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error);
            await DisplayAlertAsync(T("Title"), T("Error"), LocalizationService.TranslateKey("AiBank.Ok"));
        }
        finally { _goingBack = false; _back.IsEnabled = !_busy; }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _activity.IsRunning = busy;
        _activity.IsVisible = busy;
        _retry.IsEnabled = !busy;
        _back.IsEnabled = !busy && !_goingBack;
        foreach (var card in _cards) card.CanInteract = !busy;
        _approveCommand.ChangeCanExecute();
        _rejectCommand.ChangeCanExecute();
        UpdateSelectionControls();
    }

    private void UpdateCount()
    {
        _status.Text = string.Format(CultureInfo.CurrentCulture, T("Count"), _cards.Count);
        _empty.IsVisible = _cards.Count == 0;
        UpdateSelectionControls();
    }

    private string[] SelectedIds() => _cards.Where(card => card.IsSelected).Select(card => card.Pending.Id).ToArray();

    private void SelectAll(bool selected)
    {
        if (_busy || _goingBack) return;
        _changingSelection = true;
        try { foreach (var card in _cards) card.IsSelected = selected; }
        finally { _changingSelection = false; UpdateSelectionControls(); }
    }

    private void UpdateSelectionControls()
    {
        if (_changingSelection) return;
        int selected = _cards.Count(card => card.IsSelected);
        _selectionCount.Text = string.Format(CultureInfo.CurrentCulture, T("SelectedCount"), selected, _cards.Count);
        _selectAll.Caption = T("SelectAll");
        _clearSelection.Caption = T("ClearSelection");
        _approveSelected.Caption = string.Format(CultureInfo.CurrentCulture, T("ApproveSelected"), selected);
        _rejectSelected.Caption = string.Format(CultureInfo.CurrentCulture, T("RejectSelected"), selected);
        _approveAll.Caption = string.Format(CultureInfo.CurrentCulture, T("ApproveAll"), _cards.Count);
        _rejectAll.Caption = string.Format(CultureInfo.CurrentCulture, T("RejectAll"), _cards.Count);
        bool canAct = !_busy && !_goingBack;
        _selectAll.IsEnabled = canAct && selected < _cards.Count;
        _clearSelection.IsEnabled = _approveSelected.IsEnabled = _rejectSelected.IsEnabled = canAct && selected > 0;
        _approveAll.IsEnabled = _rejectAll.IsEnabled = canAct && _cards.Count > 0;
        // The Windows primary style can retain its saturated fill when disabled.
        // Make unavailable approval actions visibly distinct as well as inaccessible.
        _approveSelected.Opacity = _approveSelected.IsEnabled ? 1 : 0.45;
        _approveAll.Opacity = _approveAll.IsEnabled ? 1 : 0.45;
    }

    private async Task RefreshAsync()
    {
        if (_busy || _goingBack) return;
        SetBusy(true);
        _retry.IsVisible = false;
        _feedback.IsVisible = false;
        _empty.IsVisible = false;
        try
        {
            var rows = await _store.GetPendingReviewsAsync();
            _cards.Clear();
            foreach (var pending in rows)
                _cards.Add(new(pending, pending.Draft.ToWordProblem(pending.Contract).ProblemText,
                    OpenQuestionProse.Example(pending.Contract).ToWordProblem(pending.Contract).ProblemText,
                    pending.Contract.Solution, QuestionAuthoringChoices.FamilyLabel(pending.Contract.Family, AppLanguageManager.CurrentLanguage),
                    UpdateSelectionControls) { CanInteract = false });
            UpdateCount();
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e);
            _feedback.Text = T("Error");
            _feedback.IsVisible = true;
            _retry.IsVisible = true;
        }
        finally { SetBusy(false); }
    }

    private async Task ActAsync(Card card, bool approve)
    {
        if (_busy || _goingBack || card is null || !_cards.Contains(card)) return;
        await ResolveAsync([card.Pending.Id], approve, approve);
    }

    private async Task ResolveAsync(IReadOnlyList<string> requestedIds, bool approve, bool confirm)
    {
        if (_busy || _goingBack) return;
        // Freeze explicit IDs before the confirmation await. New queue arrivals
        // can never become part of an already-confirmed action.
        string[] ids = requestedIds.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return;
        SetBusy(true);
        try
        {
            if (confirm)
            {
                string message = string.Format(CultureInfo.CurrentCulture, T(approve ? "Confirm" : "ConfirmReject"), ids.Length);
                if (!await DisplayAlertAsync(T("Title"), message, T(approve ? "ApproveAction" : "RejectAction"),
                    LocalizationService.TranslateKey("AiBank.Cancel"))) return;
            }
            var result = await _store.ResolveReviewsAsync(ids, approve);
            var removed = new HashSet<string>(result.ApprovedIds, StringComparer.Ordinal);
            removed.UnionWith(result.RejectedIds);
            removed.UnionWith(result.MissingIds);
            // Do not clear/reload the collection: preserve virtualization, the
            // remaining selections and the viewport of a review deep in the queue.
            for (int index = _cards.Count - 1; index >= 0; index--)
                if (removed.Contains(_cards[index].Pending.Id)) _cards.RemoveAt(index);
            UpdateCount();
            string feedback = string.Format(CultureInfo.CurrentCulture, T("BatchResult"), result.ApprovedIds.Count, result.RejectedIds.Count);
            if (result.MissingIds.Count > 0)
                feedback += " " + string.Format(CultureInfo.CurrentCulture, T("MissingResult"), result.MissingIds.Count);
            ShowFeedback(feedback);
            // Notifications describe only rows this committed operation resolved.
            // A caller's presentation failure cannot turn a saved batch into a retry.
            foreach (string id in result.ApprovedIds) NotifyResolved(id, true);
            foreach (string id in result.RejectedIds) NotifyResolved(id, false);
        }
        catch (OperationCanceledException)
        {
            ShowFeedback(T("Cancelled"));
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e);
            ShowFeedback(T("Error"));
            await DisplayAlertAsync(T("Title"), T("Error"), LocalizationService.TranslateKey("AiBank.Ok"));
        }
        finally { SetBusy(false); }
    }

    private void ShowFeedback(string message)
    {
        _feedback.Text = message;
        _feedback.IsVisible = true;
        try { SemanticScreenReader.Default.Announce(message); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
    }

    private void NotifyResolved(string id, bool approved)
    {
        try { _onResolved?.Invoke(id, approved); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
    }
}
