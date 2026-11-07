using MathSolver.Services;
namespace MathSolver.Controls;

internal sealed class ExcelGuidePlayer : GuideMotionView
{
    private readonly Image _sheet = Artwork("excel_motion_sheet.png"), _pencil = Artwork("excel_motion_pencil.png"), _poster = Artwork("excel_fill_sheet.png");
    private readonly Ink _ink = new();
    private readonly GraphicsView _inkView;
    protected override uint Duration => ExcelGuideTimeline.DurationMilliseconds;
    public ExcelGuidePlayer()
    {
        WidthRequest = 108; HeightRequest = 72; AutomationId = "ExcelGuidePlayer";
        _inkView = new GraphicsView { Drawable = _ink, InputTransparent = true };
        AutomationProperties.SetIsInAccessibleTree(_inkView, false);
        Children.Add(_sheet); Children.Add(_inkView); Children.Add(_pencil); Children.Add(_poster);
        ResetFrame();
    }
    protected override void Apply(double progress)
    {
        var frame = ExcelGuideTimeline.At(progress);
        _poster.Opacity = frame.PosterOpacity; _sheet.Opacity = _inkView.Opacity = 1 - frame.PosterOpacity;
        _pencil.Opacity = frame.PencilOpacity;
        _pencil.TranslationX = (frame.TipX - ExcelGuideTimeline.PencilTipX) * Width;
        _pencil.TranslationY = (frame.TipY - ExcelGuideTimeline.PencilTipY) * Height;
        _ink.Frame = frame; _inkView.Invalidate();
    }
    protected override void ResetFrame()
    {
        _poster.Opacity = 1; _sheet.Opacity = _pencil.Opacity = _inkView.Opacity = 0;
        _pencil.TranslationX = _pencil.TranslationY = 0;
    }
    private sealed class Ink : IDrawable
    {
        public ExcelGuideFrame Frame;
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.StrokeColor = Color.FromArgb("#45575C"); canvas.StrokeSize = .8f; canvas.StrokeLineCap = LineCap.Round;
            void Stroke(double p, Func<double, double> x, Func<double, double> y)
            {
                if (p > 0) canvas.DrawLine((float)(x(0) * dirtyRect.Width), (float)(y(0) * dirtyRect.Height),
                    (float)(x(p) * dirtyRect.Width), (float)(y(p) * dirtyRect.Height));
            }
            Stroke(Frame.FirstInk, ExcelGuideTimeline.FirstX, ExcelGuideTimeline.FirstY);
            Stroke(Frame.SecondInk, ExcelGuideTimeline.SecondX, ExcelGuideTimeline.SecondY);
        }
    }
}
