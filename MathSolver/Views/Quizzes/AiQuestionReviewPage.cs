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
    private readonly LiveWallpaperView _wallpaper = new();
    private readonly Command<Card> _approveCommand;
    private readonly Command<Card> _rejectCommand;
    private bool _busy;
    private bool _goingBack;
    private static string T(string key) => LocalizationService.TranslateKey("AiBank.Review." + key);
    private sealed record Card(PendingQuestionReview Pending, string Candidate, string Reference, string Calculation, string Heading);

    public AiQuestionReviewPage(QuestionBankStore store, Action<string, bool>? onResolved = null)
    {
        _store = store;
        _onResolved = onResolved;
        Title = T("Title");
        Shell.SetNavBarIsVisible(this, true);
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

        var title = Text(size: 17, bold: true);
        title.Text = T("Title");
        title.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        _back.SetDynamicResource(StyleProperty, "RoundPlatformBackButtonStyle");
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
            WidthRequest = 20, HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        backIcon.SetBinding(OpacityProperty, new Binding(nameof(Opacity), source: _back));
        AutomationProperties.SetIsInAccessibleTree(backIcon, false);
        var backHost = new Grid { WidthRequest = 48, HeightRequest = 48 };
        backHost.Add(_back);
        backHost.Add(backIcon);
        var navigation = new Grid
        {
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 12,
            Margin = new Thickness(0, 10), HorizontalOptions = LayoutOptions.Center
        };
        navigation.Add(backHost, 0);
        navigation.Add(title, 1);
        ConstrainWidth(navigation);
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
            Spacing = 12, Margin = new Thickness(0, 8, 0, 16),
            HorizontalOptions = LayoutOptions.Center,
            Children = { count, hint, progress, _empty, _retry }
        };
        // Constrain the reading column, never the scrolling viewport. This keeps
        // the native scrollbar (including Windows arrow buttons) at the page edge.
        ConstrainWidth(header);
        _items.Header = header;
        _items.Footer = new BoxView { HeightRequest = 24, InputTransparent = true, Color = Colors.Transparent };
        _items.ItemsSource = _cards;
        _items.ItemTemplate = new DataTemplate(CreateCard);
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        Grid.SetRowSpan(_wallpaper, 2);
        root.Add(_wallpaper);
        root.Add(navigation, 0, 0);
        root.Add(_items, 0, 1);
        Content = root;
    }

    private View CreateCard()
    {
        var heading = Text(size: 16, bold: true);
        heading.SetBinding(Label.TextProperty, nameof(Card.Heading));
        heading.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
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
        content.Add(heading);
        content.Add(comparison);
        content.Add(Section("Calculation", nameof(Card.Calculation)));
        content.Add(actions);
        actions.SetBinding(WidthRequestProperty, new Binding(nameof(Width), source: content,
            converter: new ReadingWidthConverter(600, 0)));

        var card = new Border { Content = content, HorizontalOptions = LayoutOptions.Center };
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
        var button = new Button { Text = icon + " " + T(key), Command = command, AutomationId = automationId };
        SemanticProperties.SetDescription(button, T(key));
        button.SetDynamicResource(StyleProperty, style);
        button.MinimumHeightRequest = 48;
        button.Padding = new Thickness(16, 12);
        button.FontSize = 14;
        button.LineBreakMode = LineBreakMode.WordWrap;
        button.SetBinding(Button.CommandParameterProperty, ".");
        return button;
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
        _wallpaper.Resume();
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        _wallpaper.Pause();
        base.OnDisappearing();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (_goingBack) return;
        _goingBack = true;
        _back.IsEnabled = false;
        try { await Navigation.PopAsync(); }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error);
            await DisplayAlertAsync(T("Title"), T("Error"), LocalizationService.TranslateKey("AiBank.Ok"));
        }
        finally { _goingBack = false; _back.IsEnabled = true; }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _activity.IsRunning = busy;
        _activity.IsVisible = busy;
        _retry.IsEnabled = !busy;
        _approveCommand.ChangeCanExecute();
        _rejectCommand.ChangeCanExecute();
    }

    private void UpdateCount()
    {
        _status.Text = string.Format(CultureInfo.CurrentCulture, T("Count"), _cards.Count);
        _empty.IsVisible = _cards.Count == 0;
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
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
                    pending.Contract.Solution, QuestionAuthoringChoices.FamilyLabel(pending.Contract.Family, AppLanguageManager.CurrentLanguage)));
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
        if (_busy || card is null) return;
        SetBusy(true);
        try
        {
            bool saved = false;
            if (approve)
            {
                if (!await DisplayAlertAsync(T("Title"), T("Confirm"), T("ApproveAction"), LocalizationService.TranslateKey("AiBank.Cancel"))) return;
                saved = await _store.ApproveReviewAsync(card.Pending.Id);
            }
            else await _store.RejectReviewAsync(card.Pending.Id);
            saved |= await _store.ContainsProseAsync(new(card.Pending.Contract, card.Pending.Draft,
                card.Pending.RawJson, card.Pending.ModelName, card.Pending.CreatedUtc));
            if (approve && !saved) throw new InvalidOperationException("ReviewApprovalNotSaved");
            // Keep the remaining collection intact so resolving a card deep in
            // the queue does not reload the list or jump back to its beginning.
            _cards.Remove(card);
            UpdateCount();
            _feedback.Text = T(saved ? "Approved" : "Discarded");
            _feedback.IsVisible = true;
            SemanticScreenReader.Default.Announce(_feedback.Text);
            _onResolved?.Invoke(card.Pending.Id, saved);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(e);
            await DisplayAlertAsync(T("Title"), T("Error"), LocalizationService.TranslateKey("AiBank.Ok"));
        }
        finally { SetBusy(false); }
    }
}
