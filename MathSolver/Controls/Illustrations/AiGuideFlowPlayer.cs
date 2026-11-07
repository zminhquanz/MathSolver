using MathSolver.Services;
namespace MathSolver.Controls;

/// <summary>Decorative paper transfer, never tied to a real AI job or save operation.</summary>
internal sealed class AiGuideFlowPlayer : GuideMotionView
{
    private readonly Image _create = Artwork("excel_fill_sheet.png"), _review = Artwork("excel_review_sheet.png"),
        _tray = Artwork("review_empty_tray.png"), _saved = Artwork("excel_review_sheet.png"),
        _traveller = Artwork("excel_motion_sheet.png"), _checkedTraveller = Artwork("excel_review_sheet.png");
    protected override uint Duration => 2400;
    public AiGuideFlowPlayer()
    {
        WidthRequest = 288; HeightRequest = 80; AutomationId = "AiGuideFlowPlayer";
        for (int i = 0; i < 3; i++) ColumnDefinitions.Add(new(GridLength.Star));
        Children.Add(_create); Children.Add(_review); Children.Add(_tray);
        Grid.SetColumn(_review, 1); Grid.SetColumn(_tray, 2);
        _saved.WidthRequest = 58; _saved.HeightRequest = 44; _saved.TranslationY = -7;
        Grid.SetColumn(_saved, 2); Children.Add(_saved);
        foreach (var image in new[] { _traveller, _checkedTraveller })
        {
            image.WidthRequest = 88; image.HeightRequest = 64;
            image.HorizontalOptions = LayoutOptions.Start; image.VerticalOptions = LayoutOptions.Center;
            Grid.SetColumnSpan(image, 3); Children.Add(image);
        }
        ResetFrame();
    }
    protected override void Apply(double progress)
    {
        double p = Math.Clamp(progress, 0, 1), stage;
        if (p < .15) stage = 0;
        else if (p < .45) stage = ExcelGuideTimeline.Ease((p - .15) / .30);
        else if (p < .6) stage = 1;
        else if (p < .9) stage = 1 + ExcelGuideTimeline.Ease((p - .6) / .30);
        else stage = 2;
        double fade = Math.Clamp(p / .1, 0, 1) * (1 - Math.Clamp((p - .9) / .1, 0, 1));
        double checkedPaper = Math.Clamp((p - .42) / .12, 0, 1);
        _traveller.Opacity = fade * (1 - checkedPaper); _checkedTraveller.Opacity = fade * checkedPaper;
        _traveller.TranslationX = _checkedTraveller.TranslationX = (Width / 3 - 88) / 2 + stage * Width / 3;
        _traveller.TranslationY = _checkedTraveller.TranslationY = stage > 1 ? -7 * (stage - 1) : 0;
        _create.Opacity = _review.Opacity = .35 + .65 * (1 - fade); _saved.Opacity = 1 - fade;
    }
    protected override void ResetFrame()
    {
        _create.Opacity = _review.Opacity = _tray.Opacity = _saved.Opacity = 1;
        _traveller.Opacity = _checkedTraveller.Opacity = 0;
        _traveller.TranslationX = _checkedTraveller.TranslationX = 0;
        _traveller.TranslationY = _checkedTraveller.TranslationY = 0;
    }
}
