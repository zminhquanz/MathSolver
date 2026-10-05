using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MathSolver.Views;

public partial class QuadraticEquationView
{
    private bool? _graphToolbarStacked;
    private bool _isGraphPreviewOpen;
    private bool _preserveGraphOnReturn;
    private Button? _graphPreviewResetButton;

    private void OnGraphToolbarSizeChanged(object? sender, EventArgs e) => UpdateGraphToolbarLayout();

    private void UpdateGraphToolbarLayout()
    {
        if (GraphToolbarGrid.Width <= 0d)
            return;

        bool stacked = ResponsiveLayoutPolicy.UseStackedLayout(GraphToolbarGrid.Width, 640d);
        if (_graphToolbarStacked == stacked)
            return;
        _graphToolbarStacked = stacked;
        GraphToolbarGrid.RowDefinitions.Clear();
        GraphToolbarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (stacked)
            GraphToolbarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetColumnSpan(GraphCaptionPanel, stacked ? 2 : 1);
        Grid.SetRow(GraphCommandsPanel, stacked ? 1 : 0);
        Grid.SetColumn(GraphCommandsPanel, stacked ? 0 : 1);
        Grid.SetColumnSpan(GraphCommandsPanel, stacked ? 2 : 1);
    }

    private async void OnEnlargeGraphClicked(object? sender, EventArgs e)
    {
        if (_isGraphPreviewOpen || !CurrentGraphHasEquation)
            return;

        _isGraphPreviewOpen = true;
        _lastGraphPointer = null;
        var page = new ContentPage { Title = GraphTitleLabel.Text };
        page.SetDynamicResource(BackgroundColorProperty, "WallpaperSurfaceColor");

        var title = new Label
        {
            Text = GraphTitleLabel.Text,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };
        title.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        var equation = new Label
        {
            Text = ResultEquationLabel.Text,
            FontSize = 16,
            LineBreakMode = LineBreakMode.WordWrap
        };
        equation.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        var caption = new VerticalStackLayout { Spacing = 4, Children = { title, equation } };

        var close = CreateGraphPreviewButton(T("Equation.Graph.ClosePreview"));
        close.SetDynamicResource(Button.BackgroundColorProperty, "WallpaperPrimarySoftColor");
        var zoomOut = CreateGraphPreviewButton("−", "Equation.Graph.ZoomOut");
        var zoomIn = CreateGraphPreviewButton("+", "Equation.Graph.ZoomIn");
        zoomOut.FontSize = zoomIn.FontSize = 22;
        var reset = CreateGraphPreviewButton($"{CurrentGraphZoomPercent}%", "Equation.Graph.ResetZoom");
        _graphPreviewResetButton = reset;
        reset.MinimumWidthRequest = 74;
        reset.SetDynamicResource(Button.BackgroundColorProperty, "WallpaperPrimarySoftColor");
        reset.SetDynamicResource(Button.TextColorProperty, "PrimaryColor");
        zoomOut.Clicked += OnGraphZoomOutClicked;
        zoomIn.Clicked += OnGraphZoomInClicked;
        reset.Clicked += OnGraphResetZoomClicked;

        var zoomControls = new Grid
        {
            ColumnDefinitions = { new(new GridLength(48)), new(GridLength.Auto), new(new GridLength(48)) },
            ColumnSpacing = 8,
            HorizontalOptions = LayoutOptions.End
        };
        zoomControls.Add(zoomOut, 0, 0);
        zoomControls.Add(reset, 1, 0);
        zoomControls.Add(zoomIn, 2, 0);
        var commands = new VerticalStackLayout
        {
            Spacing = 8, HorizontalOptions = LayoutOptions.End,
            Children = { zoomControls, close }
        };
        var header = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
            RowDefinitions = { new(GridLength.Auto) }, ColumnSpacing = 12, RowSpacing = 8
        };
        header.Add(caption, 0, 0);
        header.Add(commands, 1, 0);
        bool? headerStacked = null;
        header.SizeChanged += (_, _) =>
        {
            if (header.Width <= 0d) return;
            bool stacked = ResponsiveLayoutPolicy.UseStackedLayout(header.Width, 640d);
            if (headerStacked == stacked) return;
            headerStacked = stacked;
            header.RowDefinitions.Clear();
            header.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            if (stacked) header.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumnSpan(caption, stacked ? 2 : 1);
            Grid.SetRow(commands, stacked ? 1 : 0);
            Grid.SetColumn(commands, stacked ? 0 : 1);
            Grid.SetColumnSpan(commands, stacked ? 2 : 1);
        };

        var plotBorder = new Border
        {
            Padding = 6,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }
        };
        plotBorder.SetDynamicResource(BackgroundColorProperty, "WallpaperSurfaceAltColor");
        plotBorder.SetDynamicResource(Border.StrokeProperty, "WallpaperBorderBrush");
        var help = new Label
        {
            Text = GraphHelpLabel.Text, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap
        };
        help.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        var content = new Grid
        {
            Padding = new Thickness(12), RowSpacing = 10,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }
        };
        content.Add(header, 0, 0);
        content.Add(plotBorder, 0, 1);
        content.Add(new ScrollView { Content = help, MaximumHeightRequest = 80 }, 0, 2);
        page.Content = content;

        // Move the existing view so both sizes use the same equation, viewport,
        // pan gestures and Windows wheel handler, without competing drawables.
        double previousHeight = ParabolaGraphicsView.HeightRequest;
        GraphViewportGrid.Children.Remove(ParabolaGraphicsView);
        Grid.SetRow(ParabolaGraphicsView, 0);
        ParabolaGraphicsView.HeightRequest = -1d;
        plotBorder.Content = ParabolaGraphicsView;

        var application = Application.Current;
        void UpdatePreviewTheme(object? _, AppThemeChangedEventArgs __)
        {
            ApplyCurrentGraphTheme();
            ParabolaGraphicsView.Invalidate();
        }
        if (application is not null)
            application.RequestedThemeChanged += UpdatePreviewTheme;

        void RestoreGraph()
        {
            if (!_isGraphPreviewOpen) return;
            if (application is not null)
                application.RequestedThemeChanged -= UpdatePreviewTheme;
            plotBorder.Content = null;
            Grid.SetRow(ParabolaGraphicsView, 1);
            GraphViewportGrid.Children.Add(ParabolaGraphicsView);
            ParabolaGraphicsView.HeightRequest = previousHeight;
            _lastGraphPointer = null;
            _graphPreviewResetButton = null;
            _isGraphPreviewOpen = false;
            UpdateGraphStatus();
            ParabolaGraphicsView.Invalidate();
            GraphEnlargeButton.Focus();
        }
        bool closing = false;
        close.Clicked += async (_, _) =>
        {
            if (closing) return;
            closing = true;
            try { await Navigation.PopModalAsync(); }
            finally { closing = false; }
        };
        // Covers the close button and Android's system Back action.
        page.Disappearing += (_, _) => RestoreGraph();
        try
        {
            await Navigation.PushModalAsync(page);
#if WINDOWS
            // The calculator may have unloaded while its modal was presented.
            AttachWindowsGraphMouseWheel();
#endif
            ApplyCurrentGraphTheme();
            ParabolaGraphicsView.Invalidate();
        }
        catch
        {
            RestoreGraph();
            throw;
        }
    }

    private static Button CreateGraphPreviewButton(string text, string? descriptionKey = null)
    {
        var button = new Button
        {
            Text = text, MinimumHeightRequest = 48, Padding = new Thickness(10, 8),
            CornerRadius = 10, FontSize = 15, FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        };
        button.SetDynamicResource(Button.BackgroundColorProperty, "WallpaperSurfaceAltColor");
        button.SetDynamicResource(Button.TextColorProperty, "WallpaperTextPrimaryColor");
        SemanticProperties.SetDescription(button, descriptionKey is null
            ? text : T(descriptionKey));
        return button;
    }
}
