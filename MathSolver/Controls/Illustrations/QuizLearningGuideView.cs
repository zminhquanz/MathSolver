using MathSolver.Graphics;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Controls;

/// <summary>Explicit post-answer teaching disclosure; all important information is native app text.</summary>
internal sealed class QuizLearningGuideView : VerticalStackLayout
{
    private readonly Button _toggle = new() { FontSize = 15, HorizontalOptions = LayoutOptions.Fill };
    private readonly VerticalStackLayout _body = new() { Spacing = 8, IsVisible = false };
    private readonly Label _target = new() { FontSize = 17, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label _steps = new() { FontSize = 15, LineBreakMode = LineBreakMode.WordWrap };
    private readonly FractionExpressionView _fraction = new() { MathFontSize = 23, WrapContent = true, HorizontalTextAlignment = TextAlignment.Center };
    private LearningGuidePlayer? _player;
    private LearningGuideScene? _scene;
    private Func<string, string> _translate = key => key;
    internal bool IsExpanded => _body.IsVisible;

    public QuizLearningGuideView()
    {
        Spacing = 10; IsVisible = false; AutomationId = "QuizLearningGuide";
        _toggle.AutomationId = "QuizLearningGuideToggle";
        _toggle.SetDynamicResource(VisualElement.StyleProperty, "ThemeSecondaryButtonStyle");
        ActionIcon.SetKey(_toggle, "Help");
        _target.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        _steps.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        _fraction.SetDynamicResource(FractionExpressionView.MathColorProperty, "WallpaperTextPrimaryColor");
        _body.Add(_target); _body.Add(_fraction); _body.Add(_steps);
        Add(_toggle); Add(_body);
        _toggle.Clicked += (_, _) => Toggle();
        Unloaded += (_, _) => Collapse();
    }
    internal void Configure(LearningGuideScene? scene, Func<string, string> translate)
    {
        _translate = translate;
        if (_scene != scene)
        {
            Collapse();
            if (_player is not null) _body.Remove(_player);
            _scene = scene; _player = scene is null ? null : new LearningGuidePlayer(scene);
            if (_player is not null) _body.Insert(1, _player);
        }
        IsVisible = scene is not null;
        RefreshText();
    }
    internal void Toggle()
    {
        if (_scene is null || !IsVisible) return;
        if (_body.IsVisible) Collapse();
        else
        {
            _body.IsVisible = true;
            // The clock's top-of-hour is a genuine static case, not an excuse for decorative motion.
            if (_scene.HasMotion) _player?.RequestPlay();
        }
        RefreshText();
    }
    internal void Collapse()
    {
        _player?.Stop(); _body.IsVisible = false;
        RefreshText();
    }
    private void RefreshText()
    {
        string T(string key) => _translate("Quiz.LearningGuide." + key);
        _toggle.Text = T(_body.IsVisible ? "Hide" : "Show");
        SemanticProperties.SetDescription(_toggle, _toggle.Text);
        if (_scene is null) return;
        bool clock = _scene.Kind == LearningGuideKind.Clock;
        _fraction.IsVisible = !clock;
        _fraction.Expression = clock ? "" : $"{_scene.First}/{_scene.Second}";
        _target.Text = clock ? string.Format(CultureInfo.CurrentCulture, T("Clock.Target"), _scene.First, _scene.Second) : T("Fraction.Target");
        _steps.Text = clock
            ? string.Format(CultureInfo.CurrentCulture, T(_scene.Second == 0 ? "Clock.OnHour" : "Clock.Steps"), _scene.First, _scene.Second, _scene.DialHour)
            : string.Format(CultureInfo.CurrentCulture, T("Fraction.Steps"), _scene.Second, _scene.First);
        _player?.RefreshDrawing();
    }
}

internal sealed class LearningGuidePlayer : GuideMotionView
{
    private readonly LearningGuideScene _scene;
    private readonly LearningGuideDrawable _drawing;
    private readonly GraphicsView _view;
    protected override uint Duration => 2400; // Educational sequence, not a UI transition.
    public LearningGuidePlayer(LearningGuideScene scene)
    {
        _scene = scene; _drawing = new(scene);
        _view = new GraphicsView { Drawable = _drawing, InputTransparent = true };
        HorizontalOptions = LayoutOptions.Fill;
        HeightRequest = scene.Kind == LearningGuideKind.Clock ? 260 : 96;
        AutomationId = "QuizLearningGuidePlayer";
        AutomationProperties.SetExcludedWithChildren(_view, true);
        Children.Add(_view);
    }
    protected override void Apply(double progress) { _drawing.Frame = _scene.At(progress); _view.Invalidate(); }
    protected override void ResetFrame() { _drawing.Frame = _scene.At(1); _view.Invalidate(); }
    internal void RefreshDrawing() => _view.Invalidate();
}
