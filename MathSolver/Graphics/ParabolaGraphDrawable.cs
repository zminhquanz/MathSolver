using MathSolver.Services;
using MathSolver.Numerics;
using System.Globalization;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;

namespace MathSolver.Graphics;

/// <summary>
/// Vẽ parabol theo dạng minh họa SGK:
/// trục Ox, Oy có mũi tên; trục đối xứng nét đứt;
/// đỉnh, nghiệm thực và nhãn -b/(2a).
/// Hệ số đầu vào là Int128. Biệt thức Δ được giữ chính xác bằng BigInteger;
/// các phép tính hình học, nghiệm và viewport dùng OctoDouble. Riêng dãy điểm
/// sample để redraw đường cong được chiếu sang double một lần rồi tính theo
/// SIMD (x86 hoặc ARM64 NEON) vì GraphicsView cuối cùng cũng vẽ theo pixel.
/// </summary>
public sealed class ParabolaGraphDrawable : IDrawable
{
    private const int MinimumSampleCount =
        1024;

    private const int MaximumSampleCount =
        2048;

    private const double MinimumZoom =
        0.125d;

    private const double MaximumZoom =
        64d;

    private const double ZoomStep =
        1.5d;

    private const double WheelZoomStep =
        1.20d;

    // Chừa lề ngang bằng nhau để vùng vẽ sát và cân đối ở hai bên.
    // Nhãn trục Y sẽ tự chuyển sang bên phải khi trục nằm gần mép trái.
    private const float PlotLeftPadding =
        28f;

    private const float PlotRightPadding =
        28f;

    private const float AxisEdgeLabelWidth =
        90f;

    private Color _curveColor =
        Color.FromArgb(
            "#111827");

    private Color _axisColor =
        Color.FromArgb(
            "#111827");

    private Color _guideColor =
        Color.FromArgb(
            "#64748B");

    private Color _textColor =
        Color.FromArgb(
            "#334155");

    private Color _rootColor =
        Color.FromArgb(
            "#DC2626");

    private Color _vertexColor =
        Color.FromArgb(
            "#7C3AED");

    private bool _isDarkTheme;

    private double[] _xSamples =
        [];

    private double[] _ySamples =
        [];

    private OctoDouble _a;
    private OctoDouble _b;
    private OctoDouble _c;

    // Hệ số double chỉ dùng cho dãy sample khi redraw. Các giá trị này được
    // chiếu một lần từ hệ số OctoDouble đã chuẩn hóa; nghiệm/đỉnh/viewport
    // vẫn giữ đường OctoDouble độ chính xác cao.
    private double _drawA;
    private double _drawB;
    private double _drawC;

    // Hệ số gốc và Δ chính xác dùng riêng cho marker nghiệm. Hệ số _a/_b/_c
    // phía trên vẫn được chuẩn hóa để viewport đồ thị không phụ thuộc độ lớn.
    private OctoDouble _rootA;
    private OctoDouble _rootB;
    private OctoDouble _rootC;
    private BigInteger _exactDiscriminant;

    private double _baseCenterX;
    private double _baseHalfRangeX =
        5d;

    private double _baseCenterY;
    private double _baseHalfRangeY =
        5d;

    private double _panX;
    private double _panY;

    private double _zoom =
        1d;

    private double _vertexX;
    private double _vertexY;

    private double? _firstRoot;
    private double? _secondRoot;

    private float _lastPlotWidth;
    private float _lastPlotHeight;

    private double _lastXSpan;
    private double _lastYSpan;

    private float _lastPlotLeft;
    private float _lastPlotTop;

    private double _lastXMinimum;
    private double _lastXMaximum;
    private double _lastYMinimum;
    private double _lastYMaximum;

    public bool HasEquation { get; private set; }

    public int ZoomPercent =>
        (int)Math.Round(
            _zoom *
            100d);

    public void SetDarkTheme(
        bool isDarkTheme)
    {
        if (_isDarkTheme ==
            isDarkTheme)
        {
            return;
        }

        _isDarkTheme =
            isDarkTheme;

        if (isDarkTheme)
        {
            // Giao diện tối: parabol, trục và chữ chuyển sang trắng
            // để tương phản rõ với nền SurfaceAltColor tối.
            _curveColor =
                Color.FromArgb(
                    "#FFFFFF");

            _axisColor =
                Color.FromArgb(
                    "#F8FAFC");

            _guideColor =
                Color.FromArgb(
                    "#94A3B8");

            _textColor =
                Color.FromArgb(
                    "#F8FAFC");

            _rootColor =
                Color.FromArgb(
                    "#FB7185");

            _vertexColor =
                Color.FromArgb(
                    "#C084FC");

            return;
        }

        // Giao diện sáng giữ màu vẽ hiện tại.
        _curveColor =
            Color.FromArgb(
                "#111827");

        _axisColor =
            Color.FromArgb(
                "#111827");

        _guideColor =
            Color.FromArgb(
                "#64748B");

        _textColor =
            Color.FromArgb(
                "#334155");

        _rootColor =
            Color.FromArgb(
                "#DC2626");

        _vertexColor =
            Color.FromArgb(
                "#7C3AED");
    }

    public void SetEquation(
        Int128 a,
        Int128 b,
        Int128 c)
    {
        OctoDouble rawA =
            OctoDouble.FromInt128(
                a);

        OctoDouble rawB =
            OctoDouble.FromInt128(
                b);

        OctoDouble rawC =
            OctoDouble.FromInt128(
                c);

        _rootA = rawA;
        _rootB = rawB;
        _rootC = rawC;

        BigInteger integerA = (BigInteger)a;
        BigInteger integerB = (BigInteger)b;
        BigInteger integerC = (BigInteger)c;
        _exactDiscriminant =
            integerB * integerB -
            4 * integerA * integerC;

        OctoDouble coefficientScale =
            OctoDouble.Max(
                OctoDouble.One,
                OctoDouble.Max(
                    OctoDouble.Abs(
                        rawA),
                    OctoDouble.Max(
                        OctoDouble.Abs(
                            rawB),
                        OctoDouble.Abs(
                            rawC))));

        // Chuẩn hóa cả ba hệ số bằng cùng một Quad Double. Việc này giữ
        // nguyên nghiệm và hoành độ đỉnh, đồng thời tránh viewport bị phóng
        // theo độ lớn tuyệt đối của a, b, c.
        _a =
            rawA /
            coefficientScale;

        _b =
            rawB /
            coefficientScale;

        _c =
            rawC /
            coefficientScale;

        // Projection dành riêng cho raster/sample path. Vì cả ba hệ số đã
        // được chuẩn hóa theo cùng một scale nên chuyển sang double ở đây
        // vẫn giữ hình dạng parabol và đủ xa ngưỡng overflow/underflow.
        _drawA = _a.ToDouble();
        _drawB = _b.ToDouble();
        _drawC = _c.ToDouble();

        HasEquation =
            _a.IsFinite &&
            _b.IsFinite &&
            _c.IsFinite &&
            !_a.IsZero &&
            double.IsFinite(_drawA) &&
            double.IsFinite(_drawB) &&
            double.IsFinite(_drawC) &&
            _drawA != 0d;

        _panX =
            0d;

        _panY =
            0d;

        _zoom =
            1d;

        if (!HasEquation)
        {
            return;
        }

        OctoDouble vertexX =
            -_b /
            (2d *
             _a);

        OctoDouble vertexY =
            EvaluatePrecise(
                vertexX);

        // Chỉ chiếu sang double khi đưa tọa độ toán học lên màn hình.
        _vertexX =
            vertexX.ToDouble();

        _vertexY =
            vertexY.ToDouble();

        CalculateRootMarkers();
        ConfigureInitialViewport();
    }

    public void ResetZoom()
    {
        _zoom =
            1d;

        _panX =
            0d;

        _panY =
            0d;
    }

    public void ZoomIn()
    {
        _zoom =
            Math.Min(
                MaximumZoom,
                _zoom *
                ZoomStep);
    }

    public void ZoomOut()
    {
        _zoom =
            Math.Max(
                MinimumZoom,
                _zoom /
                ZoomStep);
    }

    /// <summary>
    /// Zoom bằng con lăn tại đúng vị trí con trỏ.
    /// Điểm toán học nằm dưới con trỏ được giữ cố định sau khi zoom.
    /// </summary>
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

        double oldZoom =
            _zoom;

        double newZoom =
            zoomIn
                ? Math.Min(
                    MaximumZoom,
                    oldZoom *
                    WheelZoomStep)
                : Math.Max(
                    MinimumZoom,
                    oldZoom /
                    WheelZoomStep);

        if (Math.Abs(
                newZoom -
                oldZoom) <
            1e-12d)
        {
            return false;
        }

        double horizontalRatio =
            Math.Clamp(
                (pixelX -
                 _lastPlotLeft) /
                _lastPlotWidth,
                0d,
                1d);

        double verticalRatio =
            Math.Clamp(
                (pixelY -
                 _lastPlotTop) /
                _lastPlotHeight,
                0d,
                1d);

        double worldX =
            _lastXMinimum +
            horizontalRatio *
            _lastXSpan;

        double worldY =
            _lastYMaximum -
            verticalRatio *
            _lastYSpan;

        _zoom =
            newZoom;

        double newHalfRangeX =
            GetSafeHalfRange(
                _baseHalfRangeX /
                _zoom,
                worldX);

        double newHalfRangeY =
            GetSafeHalfRange(
                _baseHalfRangeY /
                _zoom,
                worldY);

        double newCenterX =
            worldX -
            (horizontalRatio -
             0.5d) *
            2d *
            newHalfRangeX;

        double newCenterY =
            worldY -
            (1d -
             2d *
             verticalRatio) *
            newHalfRangeY;

        double newPanX =
            newCenterX -
            _baseCenterX;

        double newPanY =
            newCenterY -
            _baseCenterY;

        if (!double.IsFinite(
                newPanX) ||
            !double.IsFinite(
                newPanY))
        {
            _zoom =
                oldZoom;

            return false;
        }

        _panX =
            newPanX;

        _panY =
            newPanY;

        return true;
    }

    /// <summary>
    /// Di chuyển khung nhìn theo khoảng rê chuột tính bằng pixel.
    /// Kéo đồ thị sang phải thì nội dung cũng đi sang phải.
    /// </summary>
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

        double oldPanX =
            _panX;

        double oldPanY =
            _panY;

        _panX -=
            deltaX /
            _lastPlotWidth *
            _lastXSpan;

        _panY +=
            deltaY /
            _lastPlotHeight *
            _lastYSpan;

        if (!double.IsFinite(
                _panX) ||
            !double.IsFinite(
                _panY))
        {
            _panX =
                oldPanX;

            _panY =
                oldPanY;

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
            DrawEmptyMessage(
                canvas,
                dirtyRect);

            return;
        }

        RectF plotRect =
            new(
                dirtyRect.X +
                PlotLeftPadding,
                dirtyRect.Y +
                24f,
                Math.Max(
                    30f,
                    dirtyRect.Width -
                    PlotLeftPadding -
                    PlotRightPadding),
                Math.Max(
                    30f,
                    dirtyRect.Height -
                    64f));

        double xHalfRange =
            GetSafeHalfRange(
                _baseHalfRangeX /
                _zoom,
                _baseCenterX +
                _panX);

        double yHalfRange =
            GetSafeHalfRange(
                _baseHalfRangeY /
                _zoom,
                _baseCenterY +
                _panY);

        double xCenter =
            _baseCenterX +
            _panX;

        double yCenter =
            _baseCenterY +
            _panY;

        double xMinimum =
            xCenter -
            xHalfRange;

        double xMaximum =
            xCenter +
            xHalfRange;

        double yMinimum =
            yCenter -
            yHalfRange;

        double yMaximum =
            yCenter +
            yHalfRange;

        _lastPlotWidth =
            plotRect.Width;

        _lastPlotHeight =
            plotRect.Height;

        _lastXSpan =
            xMaximum -
            xMinimum;

        _lastYSpan =
            yMaximum -
            yMinimum;

        _lastPlotLeft =
            plotRect.Left;

        _lastPlotTop =
            plotRect.Top;

        _lastXMinimum =
            xMinimum;

        _lastXMaximum =
            xMaximum;

        _lastYMinimum =
            yMinimum;

        _lastYMaximum =
            yMaximum;

        int sampleCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    plotRect.Width *
                    2d),
                MinimumSampleCount,
                MaximumSampleCount);

        EnsureSampleCapacity(
            sampleCount);

        FillXValues(
            sampleCount,
            xMinimum,
            xMaximum);

        // Đây là hot path của zoom/pan. Nghiệm, đỉnh và viewport đã được
        // tính bằng OctoDouble; riêng 1K-2K điểm vẽ được batch bằng double
        // SIMD để redraw nhanh hơn mà không ảnh hưởng độ chính xác toán học.
        ParabolaSimdEvaluator.Evaluate(
            _xSamples,
            _ySamples,
            sampleCount,
            _drawA,
            _drawB,
            _drawC);

        DrawTextbookAxes(
            canvas,
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);

        DrawSymmetryGuides(
            canvas,
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);

        DrawCurve(
            canvas,
            plotRect,
            sampleCount,
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

        DrawTextbookLabels(
            canvas,
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum);
    }

    private void ConfigureInitialViewport()
    {
        double featureMinimumX =
            Math.Min(
                0d,
                _vertexX);

        double featureMaximumX =
            Math.Max(
                0d,
                _vertexX);

        IncludeXFeature(
            _firstRoot,
            ref featureMinimumX,
            ref featureMaximumX);

        IncludeXFeature(
            _secondRoot,
            ref featureMinimumX,
            ref featureMaximumX);

        double featureSpanX =
            featureMaximumX -
            featureMinimumX;

        if (!double.IsFinite(
                featureSpanX) ||
            featureSpanX <= 1e-12d)
        {
            featureMinimumX -=
                4d;

            featureMaximumX +=
                4d;

            featureSpanX =
                featureMaximumX -
                featureMinimumX;
        }

        double xPadding =
            Math.Max(
                2d,
                featureSpanX *
                0.30d);

        double xMinimum =
            featureMinimumX -
            xPadding;

        double xMaximum =
            featureMaximumX +
            xPadding;

        _baseCenterX =
            (xMinimum +
             xMaximum) /
            2d;

        _baseHalfRangeX =
            Math.Max(
                2d,
                (xMaximum -
                 xMinimum) /
                2d);

        double yAtMinimum =
            EvaluateScalar(
                xMinimum);

        double yAtMaximum =
            EvaluateScalar(
                xMaximum);

        double yMinimum =
            Math.Min(
                0d,
                Math.Min(
                    yAtMinimum,
                    yAtMaximum));

        double yMaximum =
            Math.Max(
                0d,
                Math.Max(
                    yAtMinimum,
                    yAtMaximum));

        if (_vertexX >= xMinimum &&
            _vertexX <= xMaximum &&
            double.IsFinite(
                _vertexY))
        {
            yMinimum =
                Math.Min(
                    yMinimum,
                    _vertexY);

            yMaximum =
                Math.Max(
                    yMaximum,
                    _vertexY);
        }

        double ySpan =
            yMaximum -
            yMinimum;

        if (!double.IsFinite(
                ySpan) ||
            ySpan <= 1e-12d)
        {
            yMinimum =
                -4d;

            yMaximum =
                4d;

            ySpan =
                8d;
        }

        double yPadding =
            Math.Max(
                1d,
                ySpan *
                0.18d);

        yMinimum -=
            yPadding;

        yMaximum +=
            yPadding;

        _baseCenterY =
            (yMinimum +
             yMaximum) /
            2d;

        _baseHalfRangeY =
            Math.Max(
                1d,
                (yMaximum -
                 yMinimum) /
                2d);
    }

    private static void IncludeXFeature(
        double? feature,
        ref double minimum,
        ref double maximum)
    {
        if (!feature.HasValue ||
            !double.IsFinite(
                feature.Value))
        {
            return;
        }

        minimum =
            Math.Min(
                minimum,
                feature.Value);

        maximum =
            Math.Max(
                maximum,
                feature.Value);
    }

    private void CalculateRootMarkers()
    {
        _firstRoot =
            null;

        _secondRoot =
            null;

        if (_exactDiscriminant.Sign < 0)
        {
            return;
        }

        if (_exactDiscriminant.IsZero)
        {
            OctoDouble root =
                -_rootB /
                (2d *
                 _rootA);

            double projectedRoot =
                root.ToDouble();

            if (root.IsFinite &&
                double.IsFinite(
                    projectedRoot))
            {
                _firstRoot =
                    projectedRoot;
            }

            return;
        }

        OctoDouble squareRoot =
            OctoDouble.Sqrt(
                OctoDouble.FromBigInteger(
                    _exactDiscriminant));

        // Công thức q tránh triệt tiêu số khi |b| gần √Δ.
        OctoDouble q =
            -0.5d *
            (_rootB +
             OctoDouble.CopySign(
                 squareRoot,
                 _rootB));

        OctoDouble firstRoot;
        OctoDouble secondRoot;

        if (!q.IsZero)
        {
            firstRoot =
                q /
                _rootA;

            secondRoot =
                _rootC /
                q;
        }
        else
        {
            firstRoot =
                (-_rootB +
                 squareRoot) /
                (2d *
                 _rootA);

            secondRoot =
                (-_rootB -
                 squareRoot) /
                (2d *
                 _rootA);
        }

        double projectedFirstRoot =
            firstRoot.ToDouble();

        double projectedSecondRoot =
            secondRoot.ToDouble();

        if (firstRoot.IsFinite &&
            double.IsFinite(
                projectedFirstRoot))
        {
            _firstRoot =
                projectedFirstRoot;
        }

        if (secondRoot.IsFinite &&
            double.IsFinite(
                projectedSecondRoot))
        {
            _secondRoot =
                projectedSecondRoot;
        }
    }

    private void EnsureSampleCapacity(
        int sampleCount)
    {
        if (_xSamples.Length <
            sampleCount)
        {
            _xSamples =
                new double[sampleCount];

            _ySamples =
                new double[sampleCount];
        }
    }

    private void FillXValues(
        int sampleCount,
        double xMinimum,
        double xMaximum)
    {
        double step =
            sampleCount > 1
                ? (xMaximum -
                   xMinimum) /
                  (sampleCount -
                   1d)
                : 0d;

        for (int index = 0;
             index < sampleCount;
             index++)
        {
            _xSamples[index] =
                xMinimum +
                step *
                index;
        }
    }

    private void DrawTextbookAxes(
        ICanvas canvas,
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        canvas.SaveState();

        canvas.StrokeColor =
            _axisColor;

        canvas.StrokeSize =
            2.25f;

        bool hasHorizontalAxis =
            yMinimum <= 0d &&
            yMaximum >= 0d;

        bool hasVerticalAxis =
            xMinimum <= 0d &&
            xMaximum >= 0d;

        if (hasHorizontalAxis)
        {
            float axisY =
                MapY(
                    0d,
                    plotRect,
                    yMinimum,
                    yMaximum);

            float arrowX =
                plotRect.Right -
                2f;

            canvas.DrawLine(
                plotRect.Left,
                axisY,
                arrowX,
                axisY);

            DrawRightArrow(
                canvas,
                arrowX,
                axisY);

            canvas.FontColor =
                _textColor;

            canvas.FontSize =
                18f;

            canvas.DrawString(
                "x",
                plotRect.Right -
                21f,
                axisY -
                34f,
                28f,
                28f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }

        if (hasVerticalAxis)
        {
            float axisX =
                MapX(
                    0d,
                    plotRect,
                    xMinimum,
                    xMaximum);

            float arrowY =
                plotRect.Top +
                2f;

            canvas.DrawLine(
                axisX,
                plotRect.Bottom,
                axisX,
                arrowY);

            DrawUpArrow(
                canvas,
                axisX,
                arrowY);

            canvas.FontColor =
                _textColor;

            canvas.FontSize =
                18f;

            canvas.DrawString(
                "y",
                axisX +
                9f,
                plotRect.Top +
                8f,
                28f,
                28f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }

        DrawAxisTicks(
            canvas,
            plotRect,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum,
            hasHorizontalAxis,
            hasVerticalAxis);

        if (hasHorizontalAxis &&
            hasVerticalAxis)
        {
            float originX =
                MapX(
                    0d,
                    plotRect,
                    xMinimum,
                    xMaximum);

            float originY =
                MapY(
                    0d,
                    plotRect,
                    yMinimum,
                    yMaximum);

            canvas.FontColor =
                _textColor;

            canvas.FontSize =
                15f;

            canvas.DrawString(
                "O",
                originX -
                30f,
                originY +
                4f,
                26f,
                24f,
                HorizontalAlignment.Right,
                VerticalAlignment.Top);
        }

        canvas.RestoreState();
    }

    private void DrawAxisTicks(
        ICanvas canvas,
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum,
        bool hasHorizontalAxis,
        bool hasVerticalAxis)
    {
        canvas.StrokeColor =
            _axisColor;

        canvas.StrokeSize =
            1.6f;

        canvas.FontColor =
            _textColor;

        canvas.FontSize =
            13f;

        if (hasHorizontalAxis)
        {
            float axisY =
                MapY(
                    0d,
                    plotRect,
                    yMinimum,
                    yMaximum);

            double step =
                CalculateNiceTickStep(
                    xMaximum -
                    xMinimum,
                    9);

            double firstTick =
                Math.Ceiling(
                    xMinimum /
                    step) *
                step;

            bool drawAbove =
                axisY >
                plotRect.Bottom -
                38f;

            for (int index = 0;
                 index < 80;
                 index++)
            {
                double value =
                    firstTick +
                    index *
                    step;

                if (value >
                    xMaximum +
                    step *
                    0.25d)
                {
                    break;
                }

                if (Math.Abs(
                        value) <=
                    step *
                    1e-9d)
                {
                    continue;
                }

                float x =
                    MapX(
                        value,
                        plotRect,
                        xMinimum,
                        xMaximum);

                canvas.DrawLine(
                    x,
                    axisY -
                    5f,
                    x,
                    axisY +
                    5f);

                float labelY =
                    drawAbove
                        ? axisY -
                          31f
                        : axisY +
                          7f;

                canvas.DrawString(
                    FormatTickNumber(
                        value),
                    x -
                    48f,
                    labelY,
                    96f,
                    24f,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }
        }

        if (hasVerticalAxis)
        {
            float axisX =
                MapX(
                    0d,
                    plotRect,
                    xMinimum,
                    xMaximum);

            double step =
                CalculateNiceTickStep(
                    yMaximum -
                    yMinimum,
                    8);

            double firstTick =
                Math.Ceiling(
                    yMinimum /
                    step) *
                step;

            bool drawRight =
                axisX <
                plotRect.Left +
                72f;

            for (int index = 0;
                 index < 80;
                 index++)
            {
                double value =
                    firstTick +
                    index *
                    step;

                if (value >
                    yMaximum +
                    step *
                    0.25d)
                {
                    break;
                }

                if (Math.Abs(
                        value) <=
                    step *
                    1e-9d)
                {
                    continue;
                }

                float y =
                    MapY(
                        value,
                        plotRect,
                        yMinimum,
                        yMaximum);

                canvas.DrawLine(
                    axisX -
                    5f,
                    y,
                    axisX +
                    5f,
                    y);

                if (drawRight)
                {
                    canvas.DrawString(
                        FormatTickNumber(
                            value),
                        axisX +
                        9f,
                        y -
                        12f,
                        74f,
                        24f,
                        HorizontalAlignment.Left,
                        VerticalAlignment.Center);
                }
                else
                {
                    canvas.DrawString(
                        FormatTickNumber(
                            value),
                        axisX -
                        84f,
                        y -
                        12f,
                        74f,
                        24f,
                        HorizontalAlignment.Right,
                        VerticalAlignment.Center);
                }
            }
        }
    }

    private static double CalculateNiceTickStep(
        double range,
        int targetTickCount)
    {
        if (!double.IsFinite(
                range) ||
            range <= 0d)
        {
            return 1d;
        }

        double rawStep =
            range /
            Math.Max(
                2,
                targetTickCount);

        double magnitude =
            Math.Pow(
                10d,
                Math.Floor(
                    Math.Log10(
                        rawStep)));

        double normalized =
            rawStep /
            magnitude;

        double niceNormalized =
            normalized <= 1d
                ? 1d
                : normalized <= 2d
                    ? 2d
                    : normalized <= 5d
                        ? 5d
                        : 10d;

        double result =
            niceNormalized *
            magnitude;

        return double.IsFinite(
                   result) &&
               result > 0d
            ? result
            : 1d;
    }

    private static string FormatTickNumber(
        double value)
    {
        if (!double.IsFinite(
                value))
        {
            return "—";
        }

        if (Math.Abs(
                value) <
            1e-12d)
        {
            value =
                0d;
        }

        double absoluteValue =
            Math.Abs(
                value);

        string result =
            absoluteValue >=
                100_000d ||
            (absoluteValue > 0d &&
             absoluteValue <
             0.001d)
                ? value.ToString(
                    "0.##E+0",
                    CultureInfo.InvariantCulture)
                : value.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture);

        return result.Replace(
            "-",
            "−",
            StringComparison.Ordinal);
    }

    private static void DrawRightArrow(
        ICanvas canvas,
        float x,
        float y)
    {
        canvas.DrawLine(
            x,
            y,
            x -
            12f,
            y -
            8f);

        canvas.DrawLine(
            x,
            y,
            x -
            12f,
            y +
            8f);
    }

    private static void DrawUpArrow(
        ICanvas canvas,
        float x,
        float y)
    {
        canvas.DrawLine(
            x,
            y,
            x -
            8f,
            y +
            12f);

        canvas.DrawLine(
            x,
            y,
            x +
            8f,
            y +
            12f);
    }

    private void DrawSymmetryGuides(
        ICanvas canvas,
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        if (!double.IsFinite(
                _vertexX) ||
            _vertexX < xMinimum ||
            _vertexX > xMaximum)
        {
            return;
        }

        float vertexScreenX =
            MapX(
                _vertexX,
                plotRect,
                xMinimum,
                xMaximum);

        canvas.SaveState();

        canvas.StrokeColor =
            _guideColor;

        canvas.StrokeSize =
            1.2f;

        DrawDashedVerticalLine(
            canvas,
            vertexScreenX,
            plotRect.Top,
            plotRect.Bottom);

        if (xMinimum <= 0d &&
            xMaximum >= 0d &&
            _vertexY >= yMinimum &&
            _vertexY <= yMaximum)
        {
            float yAxisX =
                MapX(
                    0d,
                    plotRect,
                    xMinimum,
                    xMaximum);

            float vertexScreenY =
                MapY(
                    _vertexY,
                    plotRect,
                    yMinimum,
                    yMaximum);

            DrawDashedHorizontalLine(
                canvas,
                Math.Min(
                    yAxisX,
                    vertexScreenX),
                Math.Max(
                    yAxisX,
                    vertexScreenX),
                vertexScreenY);
        }

        canvas.RestoreState();
    }

    private static void DrawDashedVerticalLine(
        ICanvas canvas,
        float x,
        float startY,
        float endY)
    {
        const float dashLength =
            7f;

        const float gapLength =
            5f;

        for (float y = startY;
             y < endY;
             y +=
                 dashLength +
                 gapLength)
        {
            canvas.DrawLine(
                x,
                y,
                x,
                Math.Min(
                    endY,
                    y +
                    dashLength));
        }
    }

    private static void DrawDashedHorizontalLine(
        ICanvas canvas,
        float startX,
        float endX,
        float y)
    {
        const float dashLength =
            7f;

        const float gapLength =
            5f;

        for (float x = startX;
             x < endX;
             x +=
                 dashLength +
                 gapLength)
        {
            canvas.DrawLine(
                x,
                y,
                Math.Min(
                    endX,
                    x +
                    dashLength),
                y);
        }
    }

    private void DrawCurve(
        ICanvas canvas,
        RectF plotRect,
        int sampleCount,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        var path =
            new PathF();

        bool hasStarted =
            false;

        for (int index = 0;
             index < sampleCount;
             index++)
        {
            double xValue =
                _xSamples[index];

            double yValue =
                _ySamples[index];

            if (!double.IsFinite(
                    xValue) ||
                !double.IsFinite(
                    yValue))
            {
                hasStarted =
                    false;

                continue;
            }

            float screenX =
                MapX(
                    xValue,
                    plotRect,
                    xMinimum,
                    xMaximum);

            float screenY =
                MapY(
                    yValue,
                    plotRect,
                    yMinimum,
                    yMaximum);

            if (!float.IsFinite(
                    screenX) ||
                !float.IsFinite(
                    screenY))
            {
                hasStarted =
                    false;

                continue;
            }

            if (!hasStarted)
            {
                path.MoveTo(
                    screenX,
                    screenY);

                hasStarted =
                    true;
            }
            else
            {
                path.LineTo(
                    screenX,
                    screenY);
            }
        }

        canvas.SaveState();

        canvas.ClipRectangle(
            plotRect);

        canvas.StrokeColor =
            _curveColor;

        canvas.StrokeSize =
            3.6f;

        canvas.StrokeLineCap =
            LineCap.Round;

        canvas.StrokeLineJoin =
            LineJoin.Round;

        canvas.DrawPath(
            path);

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
        canvas.SaveState();

        canvas.ClipRectangle(
            plotRect);

        if (_firstRoot.HasValue)
        {
            DrawPoint(
                canvas,
                plotRect,
                _firstRoot.Value,
                0d,
                xMinimum,
                xMaximum,
                yMinimum,
                yMaximum,
                _rootColor,
                4.5f);
        }

        if (_secondRoot.HasValue &&
            (!_firstRoot.HasValue ||
             Math.Abs(
                 _secondRoot.Value -
                 _firstRoot.Value) >
             Math.Max(
                 1d,
                 Math.Abs(
                     _firstRoot.Value)) *
             1e-12d))
        {
            DrawPoint(
                canvas,
                plotRect,
                _secondRoot.Value,
                0d,
                xMinimum,
                xMaximum,
                yMinimum,
                yMaximum,
                _rootColor,
                4.5f);
        }

        DrawPoint(
            canvas,
            plotRect,
            _vertexX,
            _vertexY,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum,
            _vertexColor,
            4.5f);

        canvas.RestoreState();
    }

    private void DrawTextbookLabels(
        ICanvas canvas,
        RectF plotRect,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        canvas.FontColor =
            _textColor;

        canvas.FontSize =
            15f;

        if (_vertexX >= xMinimum &&
            _vertexX <= xMaximum)
        {
            float vertexX =
                MapX(
                    _vertexX,
                    plotRect,
                    xMinimum,
                    xMaximum);

            const float symmetryLabelWidth =
                104f;

            const float symmetryLabelHeight =
                26f;

            // Hiển thị −b/(2a) ở đầu trên của trục đối xứng,
            // thay vì đặt bên dưới trục Ox.
            float symmetryLabelX =
                Math.Clamp(
                    vertexX -
                    symmetryLabelWidth /
                    2f,
                    plotRect.Left,
                    plotRect.Right -
                    symmetryLabelWidth);

            float symmetryLabelY =
                plotRect.Top +
                3f;

            canvas.DrawString(
                "−b/(2a)",
                symmetryLabelX,
                symmetryLabelY,
                symmetryLabelWidth,
                symmetryLabelHeight,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }

        if (_vertexX >= xMinimum &&
            _vertexX <= xMaximum &&
            _vertexY >= yMinimum &&
            _vertexY <= yMaximum)
        {
            float vertexX =
                MapX(
                    _vertexX,
                    plotRect,
                    xMinimum,
                    xMaximum);

            float vertexY =
                MapY(
                    _vertexY,
                    plotRect,
                    yMinimum,
                    yMaximum);

            canvas.FontColor =
                _vertexColor;

            canvas.FontSize =
                15f;

            canvas.DrawString(
                "V",
                vertexX +
                6f,
                vertexY -
                24f,
                20f,
                20f,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);
        }

        canvas.FontColor =
            _textColor;

        canvas.FontSize =
            12f;

        // Hai cạnh plotRect có cùng padding nên hai nhãn biên
        // được neo trực tiếp vào mép trái và mép phải của vùng vẽ.
        canvas.DrawString(
            FormatAxisNumber(
                xMinimum),
            plotRect.Left,
            plotRect.Bottom +
            5f,
            AxisEdgeLabelWidth,
            18f,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        canvas.DrawString(
            FormatAxisNumber(
                xMaximum),
            plotRect.Right -
            AxisEdgeLabelWidth,
            plotRect.Bottom +
            5f,
            AxisEdgeLabelWidth,
            18f,
            HorizontalAlignment.Right,
            VerticalAlignment.Center);
    }

    private static void DrawPoint(
        ICanvas canvas,
        RectF plotRect,
        double xValue,
        double yValue,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum,
        Color color,
        float radius)
    {
        if (!double.IsFinite(
                xValue) ||
            !double.IsFinite(
                yValue) ||
            xValue < xMinimum ||
            xValue > xMaximum ||
            yValue < yMinimum ||
            yValue > yMaximum)
        {
            return;
        }

        float screenX =
            MapX(
                xValue,
                plotRect,
                xMinimum,
                xMaximum);

        float screenY =
            MapY(
                yValue,
                plotRect,
                yMinimum,
                yMaximum);

        canvas.FillColor =
            color;

        canvas.FillCircle(
            screenX,
            screenY,
            radius);
    }

    private void DrawEmptyMessage(
        ICanvas canvas,
        RectF dirtyRect)
    {
        canvas.FontColor =
            _textColor;

        canvas.FontSize =
            14f;

        canvas.DrawString(
            "Chưa có dữ liệu đồ thị.",
            dirtyRect,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private OctoDouble EvaluatePrecise(
        OctoDouble x)
    {
        // Horner Quad Double. FMA gom tích và số hạng cộng trước khi
        // kết quả được làm tròn về tám thành phần.
        return OctoDouble.FusedMultiplyAdd(
            OctoDouble.FusedMultiplyAdd(
                _a,
                x,
                _b),
            x,
            _c);
    }

    private double EvaluateScalar(
        double x)
    {
        OctoDouble result =
            EvaluatePrecise(
                new OctoDouble(
                    x));

        // GraphicsView và phép ánh xạ pixel dùng double; đây là bước chiếu
        // cuối cùng sau khi giá trị parabol đã được tính bằng Quad Double.
        return result.ToDouble();
    }

    private static double GetSafeHalfRange(
        double requestedHalfRange,
        double center)
    {
        double minimumForPrecision =
            Math.Abs(
                center) *
            1e-14d +
            1e-9d;

        double result =
            Math.Max(
                requestedHalfRange,
                minimumForPrecision);

        return double.IsFinite(
                   result) &&
               result > 0d
            ? result
            : 1d;
    }

    private static float MapX(
        double x,
        RectF plotRect,
        double xMinimum,
        double xMaximum)
    {
        double denominator =
            xMaximum -
            xMinimum;

        if (denominator == 0d ||
            !double.IsFinite(
                denominator))
        {
            return plotRect.X;
        }

        double ratio =
            (x -
             xMinimum) /
            denominator;

        return
            plotRect.X +
            (float)(
                ratio *
                plotRect.Width);
    }

    private static float MapY(
        double y,
        RectF plotRect,
        double yMinimum,
        double yMaximum)
    {
        double denominator =
            yMaximum -
            yMinimum;

        if (denominator == 0d ||
            !double.IsFinite(
                denominator))
        {
            return plotRect.Y;
        }

        double ratio =
            (y -
             yMinimum) /
            denominator;

        return
            plotRect.Bottom -
            (float)(
                ratio *
                plotRect.Height);
    }

    private static string FormatAxisNumber(
        double value)
    {
        if (!double.IsFinite(
                value))
        {
            return "—";
        }

        double absoluteValue =
            Math.Abs(
                value);

        if (absoluteValue >=
                1_000_000d ||
            (absoluteValue > 0d &&
             absoluteValue <
             0.001d))
        {
            return value.ToString(
                "0.###E+0",
                CultureInfo.InvariantCulture);
        }

        return value.ToString(
            "0.###",
            CultureInfo.InvariantCulture);
    }
}

public static class ParabolaSimdEvaluator
{
    private enum SimdPath
    {
        AvxFma,
        Avx,
        Sse2,
        NeonFma,
        ArmVector128,
        Scalar
    }

    private static readonly SimdPath SelectedPath =
        DetectBestPath();

    public static bool IsAccelerationAvailable =>
        SelectedPath != SimdPath.Scalar;

    /// <summary>
    /// Tính hàng loạt y = ax² + bx + c cho dãy sample đã ở dạng double.
    /// Đây là đường duy nhất dùng SIMD của renderer parabol. Toán học chính
    /// (Δ, nghiệm, đỉnh, viewport) vẫn nằm trong ParabolaGraphDrawable và
    /// dùng OctoDouble.
    /// </summary>
    public static void Evaluate(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        if (count < 0 ||
            xValues.Length < count ||
            yValues.Length < count)
        {
            throw new ArgumentException(
                "Mảng mẫu không đủ kích thước.");
        }

        if (count == 0)
        {
            return;
        }

        if (!CalculationAccelerationManager.UseSimd)
        {
            EvaluateScalar(
                xValues,
                yValues,
                0,
                count,
                a,
                b,
                c);

            return;
        }

        switch (SelectedPath)
        {
            case SimdPath.AvxFma:
                EvaluateAvxFma(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.Avx:
                EvaluateAvx(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.Sse2:
                EvaluateSse2(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.NeonFma:
                EvaluateNeonFma(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.ArmVector128:
                EvaluateArmVector128(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            default:
                EvaluateScalar(
                    xValues,
                    yValues,
                    0,
                    count,
                    a,
                    b,
                    c);
                return;
        }
    }

    private static SimdPath DetectBestPath()
    {
        // x86: FMA + AVX là đường Horner nhanh và chính xác nhất hiện có.
        if (Avx.IsSupported &&
            Fma.IsSupported)
        {
            return SimdPath.AvxFma;
        }

        if (Avx.IsSupported)
        {
            return SimdPath.Avx;
        }

        if (Sse2.IsSupported)
        {
            return SimdPath.Sse2;
        }

        // ARM64: AdvSimd.Arm64 expose FMLA cho Vector128<double>.
        if (AdvSimd.Arm64.IsSupported)
        {
            return SimdPath.NeonFma;
        }

        // Một số Mono build expose generic Vector128 hardware acceleration
        // nhưng không expose AdvSimd.Arm64 trực tiếp. Trên ARM64 đây vẫn là
        // managed SIMD 128-bit (NEON), chỉ không ép FMA intrinsic.
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
            Vector128.IsHardwareAccelerated)
        {
            return SimdPath.ArmVector128;
        }

        return SimdPath.Scalar;
    }

    private static void EvaluateAvxFma(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector256<double> aVector = Vector256.Create(a);
        Vector256<double> bVector = Vector256.Create(b);
        Vector256<double> cVector = Vector256.Create(c);

        int index = 0;
        int width = Vector256<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector256<double> xVector =
                Vector256.LoadUnsafe(
                    ref xValues[index]);

            // Horner + FMA3: y = fma(fma(a, x, b), x, c)
            Vector256<double> axPlusB =
                Fma.MultiplyAdd(
                    aVector,
                    xVector,
                    bVector);

            Vector256<double> yVector =
                Fma.MultiplyAdd(
                    axPlusB,
                    xVector,
                    cVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateAvx(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector256<double> aVector = Vector256.Create(a);
        Vector256<double> bVector = Vector256.Create(b);
        Vector256<double> cVector = Vector256.Create(c);

        int index = 0;
        int width = Vector256<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector256<double> xVector =
                Vector256.LoadUnsafe(
                    ref xValues[index]);

            Vector256<double> yVector =
                Avx.Add(
                    Avx.Multiply(
                        Avx.Add(
                            Avx.Multiply(
                                aVector,
                                xVector),
                            bVector),
                        xVector),
                    cVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateSse2(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector128<double> aVector = Vector128.Create(a);
        Vector128<double> bVector = Vector128.Create(b);
        Vector128<double> cVector = Vector128.Create(c);

        int index = 0;
        int width = Vector128<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector128<double> xVector =
                Vector128.LoadUnsafe(
                    ref xValues[index]);

            Vector128<double> yVector =
                Sse2.Add(
                    Sse2.Multiply(
                        Sse2.Add(
                            Sse2.Multiply(
                                aVector,
                                xVector),
                            bVector),
                        xVector),
                    cVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateNeonFma(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector128<double> aVector = Vector128.Create(a);
        Vector128<double> bVector = Vector128.Create(b);
        Vector128<double> cVector = Vector128.Create(c);

        int index = 0;
        int width = Vector128<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector128<double> xVector =
                Vector128.LoadUnsafe(
                    ref xValues[index]);

            // AArch64 FMLA, API order: addend + left * right.
            Vector128<double> axPlusB =
                AdvSimd.Arm64.FusedMultiplyAdd(
                    bVector,
                    aVector,
                    xVector);

            Vector128<double> yVector =
                AdvSimd.Arm64.FusedMultiplyAdd(
                    cVector,
                    axPlusB,
                    xVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateArmVector128(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector128<double> aVector = Vector128.Create(a);
        Vector128<double> bVector = Vector128.Create(b);
        Vector128<double> cVector = Vector128.Create(c);

        int index = 0;
        int width = Vector128<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector128<double> xVector =
                Vector128.LoadUnsafe(
                    ref xValues[index]);

            // Generic Vector128 vẫn là managed SIMD 128-bit trên ARM64.
            // Không giả FMA nếu Mono chưa expose AdvSimd.Arm64.
            Vector128<double> yVector =
                ((aVector * xVector) + bVector) * xVector + cVector;

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateScalar(
        double[] xValues,
        double[] yValues,
        int startIndex,
        int count,
        double a,
        double b,
        double c)
    {
        for (int index = startIndex;
             index < count;
             index++)
        {
            double x = xValues[index];

            yValues[index] =
                Math.FusedMultiplyAdd(
                    Math.FusedMultiplyAdd(
                        a,
                        x,
                        b),
                    x,
                    c);
        }
    }
}
