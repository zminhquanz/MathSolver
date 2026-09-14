using MathSolver.Numerics;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Graphics;

/// <summary>
/// Đồ thị tương tác cho phương trình bậc nhất ax + b = 0, biểu diễn hàm
/// y = ax + b theo kiểu SGK với trục tọa độ, giao điểm Ox và Oy.
/// Hệ số nguồn là Int128; phép tính nghiệm dùng QuadDouble, chỉ chuyển sang
/// double ở bước cuối để chiếu tọa độ lên màn hình.
/// </summary>
public sealed class LinearEquationGraphDrawable : IDrawable
{
    private const double MinimumZoom = 0.125d;
    private const double MaximumZoom = 64d;
    private const double ZoomStep = 1.5d;
    private const double WheelZoomStep = 1.20d;

    private const float PlotLeftPadding = 34f;
    private const float PlotRightPadding = 28f;
    private const float PlotTopPadding = 24f;
    private const float PlotBottomPadding = 42f;

    private Color _lineColor = Color.FromArgb("#111827");
    private Color _axisColor = Color.FromArgb("#111827");
    private Color _guideColor = Color.FromArgb("#64748B");
    private Color _textColor = Color.FromArgb("#334155");
    private Color _rootColor = Color.FromArgb("#DC2626");
    private Color _interceptColor = Color.FromArgb("#7C3AED");

    private QuadDouble _a;
    private QuadDouble _b;

    private double _rootX;
    private double _yIntercept;

    private double _baseCenterX;
    private double _baseHalfRangeX = 5d;
    private double _baseCenterY;
    private double _baseHalfRangeY = 5d;
    private double _panX;
    private double _panY;
    private double _zoom = 1d;

    private float _lastPlotWidth;
    private float _lastPlotHeight;
    private float _lastPlotLeft;
    private float _lastPlotTop;
    private double _lastXSpan;
    private double _lastYSpan;
    private double _lastXMinimum;
    private double _lastXMaximum;
    private double _lastYMinimum;
    private double _lastYMaximum;

    public bool HasEquation { get; private set; }

    public int ZoomPercent =>
        (int)Math.Round(_zoom * 100d);

    public void SetDarkTheme(bool isDarkTheme)
    {
        if (isDarkTheme)
        {
            _lineColor = Color.FromArgb("#F8FAFC");
            _axisColor = Color.FromArgb("#F8FAFC");
            _guideColor = Color.FromArgb("#94A3B8");
            _textColor = Color.FromArgb("#F8FAFC");
            _rootColor = Color.FromArgb("#FB7185");
            _interceptColor = Color.FromArgb("#C084FC");
            return;
        }

        _lineColor = Color.FromArgb("#111827");
        _axisColor = Color.FromArgb("#111827");
        _guideColor = Color.FromArgb("#64748B");
        _textColor = Color.FromArgb("#334155");
        _rootColor = Color.FromArgb("#DC2626");
        _interceptColor = Color.FromArgb("#7C3AED");
    }

    public void SetEquation(
        Int128 a,
        Int128 b)
    {
        QuadDouble rawA = QuadDouble.FromInt128(a);
        QuadDouble rawB = QuadDouble.FromInt128(b);

        _a = rawA;
        _b = rawB;

        HasEquation =
            _a.IsFinite &&
            _b.IsFinite &&
            !_a.IsZero;

        _panX = 0d;
        _panY = 0d;
        _zoom = 1d;

        if (!HasEquation)
        {
            return;
        }

        QuadDouble root = -rawB / rawA;
        _rootX = root.ToDouble();
        _yIntercept = _b.ToDouble();

        if (!double.IsFinite(_rootX) ||
            !double.IsFinite(_yIntercept))
        {
            HasEquation = false;
            return;
        }

        ConfigureInitialViewport();
    }

    public void ResetZoom()
    {
        _zoom = 1d;
        _panX = 0d;
        _panY = 0d;
    }

    public void ZoomIn() =>
        _zoom = Math.Min(MaximumZoom, _zoom * ZoomStep);

    public void ZoomOut() =>
        _zoom = Math.Max(MinimumZoom, _zoom / ZoomStep);

    public bool ZoomAtPixel(
        float pixelX,
        float pixelY,
        bool zoomIn)
    {
        if (!HasEquation ||
            _lastPlotWidth <= 0f ||
            _lastPlotHeight <= 0f ||
            _lastXSpan <= 0d ||
            _lastYSpan <= 0d)
        {
            return false;
        }

        double oldZoom = _zoom;
        double newZoom = zoomIn
            ? Math.Min(MaximumZoom, oldZoom * WheelZoomStep)
            : Math.Max(MinimumZoom, oldZoom / WheelZoomStep);

        if (Math.Abs(newZoom - oldZoom) < 1e-12d)
        {
            return false;
        }

        double horizontalRatio = Math.Clamp(
            (pixelX - _lastPlotLeft) / _lastPlotWidth,
            0d,
            1d);

        double verticalRatio = Math.Clamp(
            (pixelY - _lastPlotTop) / _lastPlotHeight,
            0d,
            1d);

        double worldX =
            _lastXMinimum + horizontalRatio * _lastXSpan;

        double worldY =
            _lastYMaximum - verticalRatio * _lastYSpan;

        _zoom = newZoom;

        double newHalfRangeX = GetSafeHalfRange(
            _baseHalfRangeX / _zoom,
            worldX);

        double newHalfRangeY = GetSafeHalfRange(
            _baseHalfRangeY / _zoom,
            worldY);

        double newCenterX =
            worldX - (horizontalRatio - 0.5d) * 2d * newHalfRangeX;

        double newCenterY =
            worldY - (1d - 2d * verticalRatio) * newHalfRangeY;

        double newPanX = newCenterX - _baseCenterX;
        double newPanY = newCenterY - _baseCenterY;

        if (!double.IsFinite(newPanX) ||
            !double.IsFinite(newPanY))
        {
            _zoom = oldZoom;
            return false;
        }

        _panX = newPanX;
        _panY = newPanY;
        return true;
    }

    public bool PanByPixels(
        float deltaX,
        float deltaY)
    {
        if (!HasEquation ||
            _lastPlotWidth <= 0f ||
            _lastPlotHeight <= 0f ||
            _lastXSpan <= 0d ||
            _lastYSpan <= 0d)
        {
            return false;
        }

        double oldPanX = _panX;
        double oldPanY = _panY;

        _panX -= deltaX / _lastPlotWidth * _lastXSpan;
        _panY += deltaY / _lastPlotHeight * _lastYSpan;

        if (!double.IsFinite(_panX) ||
            !double.IsFinite(_panY))
        {
            _panX = oldPanX;
            _panY = oldPanY;
            return false;
        }

        return true;
    }

    public void Draw(
        ICanvas canvas,
        RectF dirtyRect)
    {
        if (!HasEquation ||
            dirtyRect.Width <= 100f ||
            dirtyRect.Height <= 100f)
        {
            DrawEmptyMessage(canvas, dirtyRect);
            return;
        }

        RectF plotRect = new(
            dirtyRect.X + PlotLeftPadding,
            dirtyRect.Y + PlotTopPadding,
            Math.Max(30f, dirtyRect.Width - PlotLeftPadding - PlotRightPadding),
            Math.Max(30f, dirtyRect.Height - PlotTopPadding - PlotBottomPadding));

        double xCenter = _baseCenterX + _panX;
        double yCenter = _baseCenterY + _panY;

        double xHalfRange = GetSafeHalfRange(
            _baseHalfRangeX / _zoom,
            xCenter);

        double yHalfRange = GetSafeHalfRange(
            _baseHalfRangeY / _zoom,
            yCenter);

        double xMinimum = xCenter - xHalfRange;
        double xMaximum = xCenter + xHalfRange;
        double yMinimum = yCenter - yHalfRange;
        double yMaximum = yCenter + yHalfRange;

        CacheViewport(
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);

        DrawAxes(
            canvas,
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);

        DrawLine(
            canvas,
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);

        DrawMarkers(
            canvas,
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);
    }

    private void ConfigureInitialViewport()
    {
        double featureMinimumX = Math.Min(0d, _rootX);
        double featureMaximumX = Math.Max(0d, _rootX);
        double featureSpanX = featureMaximumX - featureMinimumX;

        if (!double.IsFinite(featureSpanX) || featureSpanX < 1e-9d)
        {
            featureMinimumX -= 4d;
            featureMaximumX += 4d;
            featureSpanX = featureMaximumX - featureMinimumX;
        }

        double xPadding = Math.Max(2d, featureSpanX * 0.35d);
        double xMinimum = featureMinimumX - xPadding;
        double xMaximum = featureMaximumX + xPadding;

        _baseCenterX = (xMinimum + xMaximum) / 2d;
        _baseHalfRangeX = Math.Max(2d, (xMaximum - xMinimum) / 2d);

        double yAtMinimum = Evaluate(xMinimum);
        double yAtMaximum = Evaluate(xMaximum);

        double yMinimum = Math.Min(0d, Math.Min(yAtMinimum, yAtMaximum));
        double yMaximum = Math.Max(0d, Math.Max(yAtMinimum, yAtMaximum));
        double ySpan = yMaximum - yMinimum;

        if (!double.IsFinite(ySpan) || ySpan < 1e-9d)
        {
            yMinimum -= 4d;
            yMaximum += 4d;
            ySpan = yMaximum - yMinimum;
        }

        double yPadding = Math.Max(1d, ySpan * 0.20d);
        yMinimum -= yPadding;
        yMaximum += yPadding;

        _baseCenterY = (yMinimum + yMaximum) / 2d;
        _baseHalfRangeY = Math.Max(1d, (yMaximum - yMinimum) / 2d);
    }

    private double Evaluate(double x)
    {
        QuadDouble preciseX = new(x);
        QuadDouble y =
            QuadDouble.FusedMultiplyAdd(
                _a,
                preciseX,
                _b);

        return y.ToDouble();
    }

    private void CacheViewport(
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        _lastPlotWidth = plotRect.Width;
        _lastPlotHeight = plotRect.Height;
        _lastPlotLeft = plotRect.Left;
        _lastPlotTop = plotRect.Top;
        _lastXSpan = xMaximum - xMinimum;
        _lastYSpan = yMaximum - yMinimum;
        _lastXMinimum = xMinimum;
        _lastXMaximum = xMaximum;
        _lastYMinimum = yMinimum;
        _lastYMaximum = yMaximum;
    }

    private void DrawAxes(
        ICanvas canvas,
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        canvas.StrokeColor = _guideColor.WithAlpha(0.28f);
        canvas.StrokeSize = 1f;

        double xStep = CalculateNiceTickStep(xMaximum - xMinimum, 8);
        double yStep = CalculateNiceTickStep(yMaximum - yMinimum, 7);

        if (xStep > 0d && double.IsFinite(xStep))
        {
            double firstX = Math.Ceiling(xMinimum / xStep) * xStep;
            for (double x = firstX; x <= xMaximum + xStep * 0.25d; x += xStep)
            {
                float px = MapX(x, plotRect, xMinimum, xMaximum);
                canvas.DrawLine(px, plotRect.Top, px, plotRect.Bottom);
            }
        }

        if (yStep > 0d && double.IsFinite(yStep))
        {
            double firstY = Math.Ceiling(yMinimum / yStep) * yStep;
            for (double y = firstY; y <= yMaximum + yStep * 0.25d; y += yStep)
            {
                float py = MapY(y, plotRect, yMinimum, yMaximum);
                canvas.DrawLine(plotRect.Left, py, plotRect.Right, py);
            }
        }

        float xAxisY = MapY(0d, plotRect, yMinimum, yMaximum);
        float yAxisX = MapX(0d, plotRect, xMinimum, xMaximum);

        xAxisY = Math.Clamp(xAxisY, plotRect.Top, plotRect.Bottom);
        yAxisX = Math.Clamp(yAxisX, plotRect.Left, plotRect.Right);

        canvas.StrokeColor = _axisColor;
        canvas.StrokeSize = 2f;
        canvas.DrawLine(plotRect.Left, xAxisY, plotRect.Right, xAxisY);
        canvas.DrawLine(yAxisX, plotRect.Top, yAxisX, plotRect.Bottom);

        DrawArrow(canvas, plotRect.Right, xAxisY, horizontal: true);
        DrawArrow(canvas, yAxisX, plotRect.Top, horizontal: false);

        canvas.FontColor = _textColor;
        canvas.FontSize = 12f;

        if (xStep > 0d && double.IsFinite(xStep))
        {
            double firstX = Math.Ceiling(xMinimum / xStep) * xStep;
            for (double x = firstX; x <= xMaximum + xStep * 0.25d; x += xStep)
            {
                if (Math.Abs(x) < xStep * 1e-6d)
                {
                    continue;
                }

                float px = MapX(x, plotRect, xMinimum, xMaximum);
                canvas.DrawString(
                    FormatTickNumber(x),
                    px - 34f,
                    Math.Min(plotRect.Bottom - 18f, xAxisY + 5f),
                    68f,
                    18f,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Top);
            }
        }

        if (yStep > 0d && double.IsFinite(yStep))
        {
            double firstY = Math.Ceiling(yMinimum / yStep) * yStep;
            for (double y = firstY; y <= yMaximum + yStep * 0.25d; y += yStep)
            {
                if (Math.Abs(y) < yStep * 1e-6d)
                {
                    continue;
                }

                float py = MapY(y, plotRect, yMinimum, yMaximum);
                canvas.DrawString(
                    FormatTickNumber(y),
                    Math.Max(plotRect.Left, yAxisX - 65f),
                    py - 9f,
                    60f,
                    18f,
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center);
            }
        }

        canvas.FontSize = 14f;
        canvas.DrawString(
            "x",
            plotRect.Right - 18f,
            xAxisY - 28f,
            24f,
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        canvas.DrawString(
            "y",
            yAxisX + 6f,
            plotRect.Top + 2f,
            24f,
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawLine(
        ICanvas canvas,
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        double y1 = Evaluate(xMinimum);
        double y2 = Evaluate(xMaximum);

        if (!double.IsFinite(y1) || !double.IsFinite(y2))
        {
            return;
        }

        float x1 = MapX(xMinimum, plotRect, xMinimum, xMaximum);
        float x2 = MapX(xMaximum, plotRect, xMinimum, xMaximum);
        float py1 = MapY(y1, plotRect, yMinimum, yMaximum);
        float py2 = MapY(y2, plotRect, yMinimum, yMaximum);

        canvas.SaveState();
        canvas.ClipRectangle(plotRect);
        canvas.StrokeColor = _lineColor;
        canvas.StrokeSize = 3f;
        canvas.DrawLine(x1, py1, x2, py2);
        canvas.RestoreState();
    }

    private void DrawMarkers(
        ICanvas canvas,
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        if (_rootX >= xMinimum && _rootX <= xMaximum &&
            0d >= yMinimum && 0d <= yMaximum)
        {
            float rootPixelX = MapX(_rootX, plotRect, xMinimum, xMaximum);
            float rootPixelY = MapY(0d, plotRect, yMinimum, yMaximum);

            canvas.FillColor = _rootColor;
            canvas.FillCircle(rootPixelX, rootPixelY, 6f);
            canvas.StrokeColor = _rootColor;
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(rootPixelX, rootPixelY, 8f);

            canvas.FontColor = _rootColor;
            canvas.FontSize = 13f;
            canvas.DrawString(
                $"x = {FormatMarkerNumber(_rootX)}",
                rootPixelX - 78f,
                rootPixelY + 10f,
                156f,
                22f,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }

        // Khi b = 0, giao điểm Oy trùng đúng nghiệm tại gốc O.
        // Chỉ vẽ marker nghiệm để tránh hai marker đè lên nhau.
        if (Math.Abs(_yIntercept) > 1e-12d &&
            0d >= xMinimum && 0d <= xMaximum &&
            _yIntercept >= yMinimum && _yIntercept <= yMaximum)
        {
            float interceptPixelX = MapX(0d, plotRect, xMinimum, xMaximum);
            float interceptPixelY = MapY(_yIntercept, plotRect, yMinimum, yMaximum);

            canvas.FillColor = _interceptColor;
            canvas.FillCircle(interceptPixelX, interceptPixelY, 5f);

            canvas.FontColor = _interceptColor;
            canvas.FontSize = 12f;
            canvas.DrawString(
                $"(0; {FormatMarkerNumber(_yIntercept)})",
                interceptPixelX + 8f,
                interceptPixelY - 24f,
                130f,
                22f,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);
        }
    }

    private void DrawEmptyMessage(
        ICanvas canvas,
        RectF dirtyRect)
    {
        canvas.FontColor = _textColor;
        canvas.FontSize = 15f;
        canvas.DrawString(
            LocalizationService.TranslateKey("Equation.Graph.Empty"),
            dirtyRect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawArrow(
        ICanvas canvas,
        float tipX,
        float tipY,
        bool horizontal)
    {
        canvas.StrokeColor = _axisColor;
        canvas.StrokeSize = 2f;

        if (horizontal)
        {
            canvas.DrawLine(tipX, tipY, tipX - 8f, tipY - 5f);
            canvas.DrawLine(tipX, tipY, tipX - 8f, tipY + 5f);
        }
        else
        {
            canvas.DrawLine(tipX, tipY, tipX - 5f, tipY + 8f);
            canvas.DrawLine(tipX, tipY, tipX + 5f, tipY + 8f);
        }
    }

    private static double GetSafeHalfRange(
        double candidate,
        double center)
    {
        double centerScale = Math.Max(1d, Math.Abs(center));
        double minimum = Math.Max(1e-12d, centerScale * 1e-12d);

        if (!double.IsFinite(candidate) || candidate < minimum)
        {
            return minimum;
        }

        return candidate;
    }

    private static double CalculateNiceTickStep(
        double span,
        int targetTickCount)
    {
        if (!double.IsFinite(span) || span <= 0d)
        {
            return 1d;
        }

        double roughStep = span / Math.Max(2, targetTickCount);
        double exponent = Math.Floor(Math.Log10(roughStep));
        double magnitude = Math.Pow(10d, exponent);
        double normalized = roughStep / magnitude;

        double nice = normalized switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d
        };

        return nice * magnitude;
    }

    private static string FormatTickNumber(double value)
    {
        double absolute = Math.Abs(value);

        if (absolute >= 1_000_000d ||
            (absolute > 0d && absolute < 0.001d))
        {
            return value.ToString("0.##E+0", CultureInfo.InvariantCulture);
        }

        return value.ToString("0.###", CultureInfo.InvariantCulture)
            .Replace("-", "−", StringComparison.Ordinal);
    }

    private static string FormatMarkerNumber(double value)
    {
        double absolute = Math.Abs(value);
        string format =
            absolute >= 10_000_000d ||
            (absolute > 0d && absolute < 0.0001d)
                ? "0.###E+0"
                : "0.######";

        return value.ToString(
                format,
                CultureInfo.InvariantCulture)
            .Replace(
                "-",
                "−",
                StringComparison.Ordinal);
    }

    private static float MapX(
        double x,
        RectF plotRect,
        double xMinimum,
        double xMaximum)
    {
        double span = xMaximum - xMinimum;
        if (!double.IsFinite(span) || span <= 0d)
        {
            return plotRect.Left;
        }

        return plotRect.Left +
               (float)((x - xMinimum) / span * plotRect.Width);
    }

    private static float MapY(
        double y,
        RectF plotRect,
        double yMinimum,
        double yMaximum)
    {
        double span = yMaximum - yMinimum;
        if (!double.IsFinite(span) || span <= 0d)
        {
            return plotRect.Bottom;
        }

        return plotRect.Bottom -
               (float)((y - yMinimum) / span * plotRect.Height);
    }
}
