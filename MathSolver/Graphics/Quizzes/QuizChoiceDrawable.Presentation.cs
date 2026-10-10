using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

internal sealed partial class QuizChoiceDrawable
{
    // Vector illustrations share the selector's 100 × 100 canvas and scale at any DPI.
    private bool DrawPresentation(ICanvas canvas)
    {
        void Box(float x, float y, float width, float height)
        {
            canvas.DrawRectangle(x, y, width, height);
            canvas.DrawLine(x + width / 2, y, x + width / 2, y + 10);
            canvas.DrawLine(x + width / 2 - 5, y + 10, x + width / 2 + 5, y + 10);
        }

        switch (id)
        {
            case "language-vietnamese":
            case "language-english":
                canvas.DrawCircle(50, 39, 28);
                canvas.DrawEllipse(38, 11, 24, 56);
                canvas.DrawLine(22, 39, 78, 39);
                canvas.DrawLine(27, 25, 73, 25);
                canvas.DrawLine(27, 53, 73, 53);
                canvas.FontSize = 22;
                canvas.DrawString(id == "language-vietnamese" ? "VI" : "EN", 12, 72, 76, 25,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                break;
            case "knowledge-money":
                canvas.DrawRectangle(10, 19, 76, 44);
                canvas.DrawRectangle(16, 25, 64, 32);
                canvas.DrawCircle(48, 41, 10);
                canvas.DrawLine(24, 36, 30, 36);
                canvas.DrawLine(66, 46, 72, 46);
                canvas.DrawEllipse(55, 71, 32, 10);
                canvas.DrawLine(55, 76, 55, 84);
                canvas.DrawLine(87, 76, 87, 84);
                canvas.DrawEllipse(55, 79, 32, 10);
                break;
            case "knowledge-packaging":
                Box(31, 14, 38, 32);
                Box(9, 53, 38, 32);
                Box(53, 53, 38, 32);
                break;
            case "knowledge-production":
                var factory = new PathF();
                factory.MoveTo(12, 65);
                factory.LineTo(12, 39);
                factory.LineTo(33, 24);
                factory.LineTo(33, 39);
                factory.LineTo(54, 24);
                factory.LineTo(54, 39);
                factory.LineTo(76, 39);
                factory.LineTo(76, 14);
                factory.LineTo(87, 14);
                factory.LineTo(87, 65);
                factory.Close();
                canvas.DrawPath(factory);
                for (int i = 0; i < 3; i++) canvas.DrawRectangle(21 + i * 23, 46, 12, 12);
                canvas.DrawLine(9, 77, 91, 77);
                canvas.DrawCircle(20, 84, 6);
                canvas.DrawCircle(80, 84, 6);
                break;
            default: return false;
        }
        return true;
    }
}
