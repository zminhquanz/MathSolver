using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

public enum GeometryShapeType
{
    Square,
    Rectangle,
    Triangle,
    Circle,
    Trapezoid,
    Rhombus,
    Parallelogram,
    Cube,
    RectangularPrism,
    Sphere,
    Cylinder,
    Cone
}

public sealed class GeometryShapeDrawable : IDrawable
{
    public GeometryShapeType ShapeType { get; init; }

    private static readonly Color ShapeColor =
        Color.FromArgb("#7C3AED");

    private static readonly Color AuxiliaryColor =
        Color.FromArgb("#94A3B8");

    private static readonly Color LabelColor =
        Color.FromArgb("#334155");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        try
        {
            canvas.StrokeColor = ShapeColor;
            canvas.StrokeSize = 2.5f;
            canvas.FontColor = LabelColor;
            canvas.FontSize = GetFontSize(dirtyRect.Width);
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;

            switch (ShapeType)
            {
                case GeometryShapeType.Square:
                    DrawSquare(canvas, dirtyRect);
                    break;

                case GeometryShapeType.Rectangle:
                    DrawRectangle(canvas, dirtyRect);
                    break;

                case GeometryShapeType.Triangle:
                    DrawTriangle(canvas, dirtyRect);
                    break;

                case GeometryShapeType.Circle:
                    DrawCircle(canvas, dirtyRect);
                    break;

                case GeometryShapeType.Trapezoid:
                    DrawTrapezoid(canvas, dirtyRect);
                    break;

                case GeometryShapeType.Rhombus:
                    DrawRhombus(canvas, dirtyRect);
                    break;

                case GeometryShapeType.Parallelogram:
                    DrawParallelogram(canvas, dirtyRect);
                    break;
                case GeometryShapeType.Cube:
                    DrawCube(canvas, dirtyRect);
                    break;
                case GeometryShapeType.RectangularPrism:
                    DrawRectangularPrism(
                        canvas,
                        dirtyRect);
                    break;
                case GeometryShapeType.Sphere:
                    DrawSphere(
                        canvas,
                        dirtyRect);
                    break;

                case GeometryShapeType.Cylinder:
                    DrawCylinder(
                        canvas,
                        dirtyRect);
                    break;

                case GeometryShapeType.Cone:
                    DrawCone(
                        canvas,
                        dirtyRect);
                    break;
            }
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private static void DrawSquare(ICanvas canvas, RectF bounds)
    {
        float side = Math.Min(
            bounds.Width * 0.48f,
            bounds.Height * 0.58f);

        float left = bounds.Center.X - side / 2;
        float top = bounds.Center.Y - side / 2;

        canvas.DrawRectangle(left, top, side, side);

        DrawHorizontalLabel(
            canvas,
            "a",
            left,
            top - 25,
            side);

        DrawVerticalLabel(
            canvas,
            "a",
            left + side + 5,
            top,
            side);
    }

    private static void DrawRectangle(ICanvas canvas, RectF bounds)
    {
        float width = bounds.Width * 0.62f;
        float height = bounds.Height * 0.40f;

        float left = bounds.Center.X - width / 2;
        float top = bounds.Center.Y - height / 2;

        canvas.DrawRectangle(left, top, width, height);

        DrawHorizontalLabel(
            canvas,
            "a",
            left,
            top - 25,
            width);

        DrawVerticalLabel(
            canvas,
            "b",
            left + width + 5,
            top,
            height);
    }

    private static void DrawTriangle(ICanvas canvas, RectF bounds)
    {
        float left = bounds.Width * 0.18f;
        float right = bounds.Width * 0.82f;
        float bottom = bounds.Height * 0.78f;
        float top = bounds.Height * 0.18f;
        float centerX = bounds.Center.X;

        PathF triangle = new();

        triangle.MoveTo(left, bottom);
        triangle.LineTo(right, bottom);
        triangle.LineTo(centerX, top);
        triangle.Close();

        canvas.DrawPath(triangle);

        DrawHorizontalLabel(
            canvas,
            "a",
            left,
            bottom + 2,
            right - left);

        DrawDashedLine(
            canvas,
            centerX,
            top,
            centerX,
            bottom);

        DrawVerticalLabel(
            canvas,
            "h",
            centerX + 4,
            top,
            bottom - top);
    }

    private static void DrawCircle(ICanvas canvas, RectF bounds)
    {
        float diameter = Math.Min(
            bounds.Width * 0.52f,
            bounds.Height * 0.62f);

        float left = bounds.Center.X - diameter / 2;
        float top = bounds.Center.Y - diameter / 2;

        canvas.DrawEllipse(left, top, diameter, diameter);

        canvas.DrawLine(
            bounds.Center.X,
            bounds.Center.Y,
            bounds.Center.X + diameter / 2,
            bounds.Center.Y);

        canvas.DrawString(
            "r",
            bounds.Center.X,
            bounds.Center.Y - 24,
            diameter / 2,
            24,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawTrapezoid(ICanvas canvas, RectF bounds)
    {
        float topLeft = bounds.Width * 0.33f;
        float topRight = bounds.Width * 0.67f;
        float bottomLeft = bounds.Width * 0.15f;
        float bottomRight = bounds.Width * 0.85f;

        float topY = bounds.Height * 0.25f;
        float bottomY = bounds.Height * 0.75f;

        PathF path = new();

        path.MoveTo(topLeft, topY);
        path.LineTo(topRight, topY);
        path.LineTo(bottomRight, bottomY);
        path.LineTo(bottomLeft, bottomY);
        path.Close();

        canvas.DrawPath(path);

        DrawHorizontalLabel(
            canvas,
            "a",
            topLeft,
            topY - 25,
            topRight - topLeft);

        DrawHorizontalLabel(
            canvas,
            "b",
            bottomLeft,
            bottomY + 2,
            bottomRight - bottomLeft);

        DrawDashedLine(
            canvas,
            topLeft,
            topY,
            topLeft,
            bottomY);

        DrawVerticalLabel(
            canvas,
            "h",
            topLeft - 30,
            topY,
            bottomY - topY);
    }

    private static void DrawRhombus(ICanvas canvas, RectF bounds)
    {
        float centerX = bounds.Center.X;
        float centerY = bounds.Center.Y;

        float horizontalRadius = bounds.Width * 0.32f;
        float verticalRadius = bounds.Height * 0.31f;

        PathF path = new();

        path.MoveTo(centerX, centerY - verticalRadius);
        path.LineTo(centerX + horizontalRadius, centerY);
        path.LineTo(centerX, centerY + verticalRadius);
        path.LineTo(centerX - horizontalRadius, centerY);
        path.Close();

        canvas.DrawPath(path);

        DrawDashedLine(
            canvas,
            centerX - horizontalRadius,
            centerY,
            centerX + horizontalRadius,
            centerY);

        DrawDashedLine(
            canvas,
            centerX,
            centerY - verticalRadius,
            centerX,
            centerY + verticalRadius);

        canvas.DrawString(
            "d₁",
            centerX,
            centerY - 28,
            horizontalRadius,
            24,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        canvas.DrawString(
            "d₂",
            centerX + 7,
            centerY,
            32,
            verticalRadius,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        canvas.DrawString(
            "a",
            centerX + horizontalRadius - 5,
            centerY - verticalRadius / 2 - 18,
            30,
            24,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawParallelogram(
        ICanvas canvas,
        RectF bounds)
    {
        float left = bounds.Width * 0.18f;
        float right = bounds.Width * 0.82f;

        float topY = bounds.Height * 0.28f;
        float bottomY = bounds.Height * 0.72f;
        float offset = bounds.Width * 0.14f;

        PathF path = new();

        path.MoveTo(left + offset, topY);
        path.LineTo(right, topY);
        path.LineTo(right - offset, bottomY);
        path.LineTo(left, bottomY);
        path.Close();

        canvas.DrawPath(path);

        DrawHorizontalLabel(
            canvas,
            "a",
            left,
            bottomY + 2,
            right - offset - left);

        DrawDashedLine(
            canvas,
            left + offset,
            topY,
            left + offset,
            bottomY);

        DrawVerticalLabel(
            canvas,
            "h",
            left + offset - 32,
            topY,
            bottomY - topY);

        canvas.DrawString(
            "b",
            right - offset / 2,
            topY + 10,
            30,
            bottomY - topY,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawHorizontalLabel(
        ICanvas canvas,
        string text,
        float x,
        float y,
        float width)
    {
        canvas.DrawString(
            text,
            x,
            y,
            width,
            24,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawVerticalLabel(
        ICanvas canvas,
        string text,
        float x,
        float y,
        float height)
    {
        canvas.DrawString(
            text,
            x,
            y,
            28,
            height,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawDashedLine(
    ICanvas canvas,
    float x1,
    float y1,
    float x2,
    float y2)
    {
        canvas.SaveState();

        canvas.StrokeColor = AuxiliaryColor;
        canvas.StrokeDashPattern = [5, 4];
        canvas.StrokeSize = 1.8f;

        canvas.DrawLine(
            x1,
            y1,
            x2,
            y2);

        canvas.RestoreState();
    }

    private static float GetFontSize(float width)
    {
        return width < 260 ? 13 : 15;
    }

    private static void DrawCube(
    ICanvas canvas,
    RectF bounds)
    {
        float scale = Math.Min(
            bounds.Width / 320f,
            bounds.Height / 200f);

        scale = Math.Clamp(
            scale,
            0.72f,
            1.1f);

        float side = 92f * scale;
        float depthX = 38f * scale;
        float depthY = 30f * scale;

        float totalWidth = side + depthX;
        float totalHeight = side + depthY;

        float left =
            bounds.Center.X - totalWidth / 2f;

        float top =
            bounds.Center.Y - totalHeight / 2f;

        // Bốn điểm của mặt trước.
        PointF frontTopLeft =
            new(left, top + depthY);

        PointF frontTopRight =
            new(left + side, top + depthY);

        PointF frontBottomLeft =
            new(left, top + depthY + side);

        PointF frontBottomRight =
            new(left + side, top + depthY + side);

        // Bốn điểm tương ứng của mặt sau.
        PointF backTopLeft =
            new(left + depthX, top);

        PointF backTopRight =
            new(left + side + depthX, top);

        PointF backBottomLeft =
            new(left + depthX, top + side);

        PointF backBottomRight =
            new(left + side + depthX, top + side);

        canvas.StrokeColor = ShapeColor;
        canvas.StrokeSize = 2.5f * scale;

        // Mặt trước.
        canvas.DrawLine(
            frontTopLeft.X,
            frontTopLeft.Y,
            frontTopRight.X,
            frontTopRight.Y);

        canvas.DrawLine(
            frontTopRight.X,
            frontTopRight.Y,
            frontBottomRight.X,
            frontBottomRight.Y);

        canvas.DrawLine(
            frontBottomRight.X,
            frontBottomRight.Y,
            frontBottomLeft.X,
            frontBottomLeft.Y);

        canvas.DrawLine(
            frontBottomLeft.X,
            frontBottomLeft.Y,
            frontTopLeft.X,
            frontTopLeft.Y);

        // Các cạnh nhìn thấy của mặt sau.
        canvas.DrawLine(
            backTopLeft.X,
            backTopLeft.Y,
            backTopRight.X,
            backTopRight.Y);

        canvas.DrawLine(
            backTopRight.X,
            backTopRight.Y,
            backBottomRight.X,
            backBottomRight.Y);

        canvas.DrawLine(
            backBottomRight.X,
            backBottomRight.Y,
            frontBottomRight.X,
            frontBottomRight.Y);

        // Các cạnh nối mặt trước và mặt sau.
        canvas.DrawLine(
            frontTopLeft.X,
            frontTopLeft.Y,
            backTopLeft.X,
            backTopLeft.Y);

        canvas.DrawLine(
            frontTopRight.X,
            frontTopRight.Y,
            backTopRight.X,
            backTopRight.Y);

        // Hai cạnh khuất được vẽ bằng nét đứt.
        canvas.SaveState();

        canvas.StrokeColor = AuxiliaryColor;
        canvas.StrokeSize = 1.7f * scale;
        canvas.StrokeDashPattern = [5f * scale, 4f * scale];

        canvas.DrawLine(
            backTopLeft.X,
            backTopLeft.Y,
            backBottomLeft.X,
            backBottomLeft.Y);

        canvas.DrawLine(
            backBottomLeft.X,
            backBottomLeft.Y,
            backBottomRight.X,
            backBottomRight.Y);

        canvas.DrawLine(
            frontBottomLeft.X,
            frontBottomLeft.Y,
            backBottomLeft.X,
            backBottomLeft.Y);

        canvas.RestoreState();

        // Chú thích cạnh a phía dưới mặt trước.
        canvas.DrawString(
            "a",
            frontBottomLeft.X,
            frontBottomLeft.Y + 4f * scale,
            side,
            25f * scale,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        // Chú thích cạnh a bên phải.
        canvas.DrawString(
            "a",
            frontBottomRight.X + 5f * scale,
            frontTopRight.Y,
            24f * scale,
            side,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        // Chú thích chiều sâu cũng bằng a.
        canvas.DrawString(
            "a",
            frontTopLeft.X,
            backTopLeft.Y - 24f * scale,
            depthX,
            24f * scale,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawRectangularPrism(
    ICanvas canvas,
    RectF rect)
    {
        float width =
            MathF.Min(rect.Width * 0.55f, 240f);

        float height =
            MathF.Min(rect.Height * 0.42f, 90f);

        float depthX = 42f;
        float depthY = 32f;

        float left =
            rect.Center.X -
            (width + depthX) / 2f;

        float top =
            rect.Center.Y -
            (height + depthY) / 2f +
            8f;

        float right =
            left + width;

        float bottom =
            top + height;

        // Mặt trước
        canvas.DrawRectangle(
            left,
            top + depthY,
            width,
            height);

        // Mặt sau
        canvas.DrawRectangle(
            left + depthX,
            top,
            width,
            height);

        // Các cạnh nối
        canvas.DrawLine(
            left,
            top + depthY,
            left + depthX,
            top);

        canvas.DrawLine(
            right,
            top + depthY,
            right + depthX,
            top);

        canvas.DrawLine(
            left,
            bottom + depthY,
            left + depthX,
            bottom);

        canvas.DrawLine(
            right,
            bottom + depthY,
            right + depthX,
            bottom);

        // Ký hiệu chiều dài
        canvas.DrawString(
            "a",
            left,
            bottom + depthY + 7f,
            width,
            25f,
            HorizontalAlignment.Center,
            VerticalAlignment.Top);

        // Ký hiệu chiều cao
        canvas.DrawString(
            "c",
            right + depthX + 8f,
            top + depthY,
            25f,
            height,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        // Ký hiệu chiều rộng
        canvas.DrawString(
            "b",
            right + 4f,
            top + 2f,
            depthX + 20f,
            depthY,
            HorizontalAlignment.Center,
            VerticalAlignment.Top);
    }

    private static void DrawSphere(
    ICanvas canvas,
    RectF rect)
    {
        float diameter =
            MathF.Min(
                rect.Width * 0.42f,
                rect.Height * 0.72f);

        float left =
            rect.Center.X -
            diameter / 2f;

        float top =
            rect.Center.Y -
            diameter / 2f;

        // Đường bao hình cầu
        canvas.DrawEllipse(
            left,
            top,
            diameter,
            diameter);

        // Đường xích đạo
        canvas.SaveState();

        canvas.StrokeColor =
            Color.FromArgb("#94A3B8");

        canvas.StrokeSize = 1.8f;

        canvas.DrawEllipse(
            left,
            rect.Center.Y - diameter * 0.15f,
            diameter,
            diameter * 0.30f);

        canvas.RestoreState();

        // Bán kính
        canvas.DrawLine(
            rect.Center.X,
            rect.Center.Y,
            left + diameter,
            rect.Center.Y);

        canvas.DrawString(
            "r",
            rect.Center.X,
            rect.Center.Y - 25f,
            diameter / 2f,
            25f,
            HorizontalAlignment.Center,
            VerticalAlignment.Bottom);
    }

    private static void DrawCylinder(
    ICanvas canvas,
    RectF rect)
    {
        float width =
            MathF.Min(rect.Width * 0.42f, 180f);

        float height =
            MathF.Min(rect.Height * 0.65f, 125f);

        float ellipseHeight = 30f;

        float left =
            rect.Center.X -
            width / 2f;

        float top =
            rect.Center.Y -
            height / 2f;

        float bottom =
            top + height;

        // Đáy phía trên
        canvas.DrawEllipse(
            left,
            top,
            width,
            ellipseHeight);

        // Hai cạnh bên
        canvas.DrawLine(
            left,
            top + ellipseHeight / 2f,
            left,
            bottom);

        canvas.DrawLine(
            left + width,
            top + ellipseHeight / 2f,
            left + width,
            bottom);

        // Đáy phía dưới
        canvas.DrawEllipse(
            left,
            bottom - ellipseHeight / 2f,
            width,
            ellipseHeight);

        // Chiều cao
        canvas.SaveState();

        canvas.StrokeColor =
            Color.FromArgb("#94A3B8");

        canvas.StrokeSize = 1.6f;

        canvas.StrokeDashPattern =
        [
            6f,
        5f
        ];

        canvas.DrawLine(
            rect.Center.X,
            top + ellipseHeight / 2f,
            rect.Center.X,
            bottom);

        canvas.RestoreState();

        canvas.DrawString(
            "h",
            rect.Center.X + 7f,
            top + ellipseHeight,
            30f,
            height - ellipseHeight,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        // Bán kính đáy
        canvas.DrawLine(
            rect.Center.X,
            bottom,
            left + width,
            bottom);

        canvas.DrawString(
            "r",
            rect.Center.X,
            bottom - 29f,
            width / 2f,
            25f,
            HorizontalAlignment.Center,
            VerticalAlignment.Bottom);
    }

    private static void DrawCone(
    ICanvas canvas,
    RectF rect)
    {
        float baseWidth =
            MathF.Min(rect.Width * 0.52f, 210f);

        float coneHeight =
            MathF.Min(rect.Height * 0.68f, 135f);

        float ellipseHeight = 30f;

        float centerX =
            rect.Center.X;

        float apexY =
            rect.Center.Y -
            coneHeight / 2f;

        float baseY =
            apexY + coneHeight;

        float left =
            centerX -
            baseWidth / 2f;

        // Hai đường sinh
        canvas.DrawLine(
            centerX,
            apexY,
            left,
            baseY);

        canvas.DrawLine(
            centerX,
            apexY,
            left + baseWidth,
            baseY);

        // Đáy hình nón
        canvas.DrawEllipse(
            left,
            baseY - ellipseHeight / 2f,
            baseWidth,
            ellipseHeight);

        // Chiều cao
        canvas.SaveState();

        canvas.StrokeColor =
            Color.FromArgb("#94A3B8");

        canvas.StrokeSize = 1.6f;

        canvas.StrokeDashPattern =
        [
            6f,
        5f
        ];

        canvas.DrawLine(
            centerX,
            apexY,
            centerX,
            baseY);

        canvas.RestoreState();

        canvas.DrawString(
            "h",
            centerX + 7f,
            apexY,
            30f,
            coneHeight,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        // Bán kính đáy
        canvas.DrawLine(
            centerX,
            baseY,
            left + baseWidth,
            baseY);

        canvas.DrawString(
            "r",
            centerX,
            baseY - 29f,
            baseWidth / 2f,
            25f,
            HorizontalAlignment.Center,
            VerticalAlignment.Bottom);

        // Đường sinh
        canvas.DrawString(
            "l",
            centerX + baseWidth * 0.20f,
            apexY + coneHeight * 0.28f,
            25f,
            25f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }
}