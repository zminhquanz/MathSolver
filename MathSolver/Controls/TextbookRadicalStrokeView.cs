namespace MathSolver.Controls;

/// <summary>
/// Vẽ dấu căn và toàn bộ gạch ngang bằng cùng một đường vector.
/// Cách này tránh sai lệch độ dày/độ cao do ghép glyph √ với BoxView,
/// đặc biệt trên Windows khi dùng tỉ lệ DPI không nguyên.
/// </summary>
public sealed class TextbookRadicalStrokeView : GraphicsView
{
    public static readonly BindableProperty LineColorProperty =
        BindableProperty.Create(
            nameof(LineColor),
            typeof(Color),
            typeof(TextbookRadicalStrokeView),
            Color.FromArgb("#16A34A"),
            propertyChanged: OnLineColorChanged);

    private readonly TextbookRadicalDrawable _drawable =
        new();

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public TextbookRadicalStrokeView()
    {
        Drawable = _drawable;
        InputTransparent = true;
    }

    private static void OnLineColorChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        var view =
            (TextbookRadicalStrokeView)bindable;

        view._drawable.LineColor =
            (Color)newValue;

        view.Invalidate();
    }

    private sealed class TextbookRadicalDrawable : IDrawable
    {
        public Color LineColor { get; set; } =
            Color.FromArgb("#16A34A");

        public void Draw(
            ICanvas canvas,
            RectF dirtyRect)
        {
            if (dirtyRect.Width <= 16f ||
                dirtyRect.Height <= 16f)
            {
                return;
            }

            float scale =
                MathF.Max(
                    1f,
                    dirtyRect.Height /
                    30f);

            float top =
                3f *
                scale;

            float bottom =
                MathF.Min(
                    dirtyRect.Height - (2f * scale),
                    27f * scale);

            float checkY =
                top +
                ((bottom - top) * 0.60f);

            var radical =
                new PathF();

            radical.MoveTo(
                1.25f * scale,
                checkY);

            radical.LineTo(
                4.25f * scale,
                checkY);

            radical.LineTo(
                8f * scale,
                bottom);

            radical.LineTo(
                15f * scale,
                top);

            // Dấu móc và gạch ngang nằm trong cùng PathF nên luôn nối liền,
            // có cùng độ dày và được rasterize trong một lần vẽ.
            radical.LineTo(
                dirtyRect.Right - 1f,
                top);

            canvas.Antialias =
                true;

            canvas.StrokeColor =
                LineColor;

            canvas.StrokeSize =
                2.2f *
                scale;

            canvas.StrokeLineCap =
                LineCap.Round;

            canvas.StrokeLineJoin =
                LineJoin.Round;

            canvas.DrawPath(
                radical);
        }
    }
}
