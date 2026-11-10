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
            case "knowledge-objects":
                // Everyday clothing: a recognizable T-shirt, not counting dots.
                var shirt = new PathF();
                shirt.MoveTo(28, 20);
                shirt.LineTo(39, 15);
                shirt.CurveTo(43, 23, 57, 23, 61, 15);
                shirt.LineTo(72, 20);
                shirt.LineTo(90, 38);
                shirt.LineTo(76, 52);
                shirt.LineTo(68, 43);
                shirt.LineTo(68, 84);
                shirt.LineTo(32, 84);
                shirt.LineTo(32, 43);
                shirt.LineTo(24, 52);
                shirt.LineTo(10, 38);
                shirt.Close();
                canvas.DrawPath(shirt);
                canvas.DrawLine(33, 77, 67, 77);
                break;
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
            case "knowledge-animals":
                // One continuous outline makes the chick clear at small icon sizes.
                var chick = new PathF();
                chick.MoveTo(24, 52);
                chick.CurveTo(30, 45, 39, 42, 48, 44);
                chick.CurveTo(48, 30, 59, 20, 71, 24);
                chick.CurveTo(84, 28, 85, 42, 79, 52);
                chick.CurveTo(76, 70, 64, 79, 47, 79);
                chick.CurveTo(29, 79, 22, 68, 24, 52);
                chick.Close();
                canvas.DrawPath(chick);
                canvas.DrawLine(24, 58, 11, 48);
                canvas.DrawLine(24, 65, 10, 60);
                canvas.FillCircle(70, 36, 3);
                var beak = new PathF();
                beak.MoveTo(82, 42);
                beak.LineTo(94, 47);
                beak.LineTo(80, 53);
                beak.Close();
                canvas.DrawPath(beak);
                canvas.DrawArc(35, 52, 24, 14, 190, 350, false, false);
                canvas.DrawLine(43, 79, 43, 88);
                canvas.DrawLine(63, 77, 63, 88);
                canvas.DrawLine(37, 88, 49, 88);
                canvas.DrawLine(57, 88, 69, 88);
                break;
            case "knowledge-school-supplies":
                // A ruled notebook and a pencil represent classroom supplies.
                canvas.DrawRoundedRectangle(13, 21, 50, 64, 4);
                canvas.DrawLine(24, 21, 24, 85);
                for (int row = 0; row < 3; row++)
                    canvas.DrawLine(32, 38 + row * 15, 55, 38 + row * 15);
                canvas.DrawLine(75, 20, 88, 27);
                canvas.DrawLine(69, 72, 82, 79);
                canvas.DrawLine(75, 20, 69, 72);
                canvas.DrawLine(88, 27, 82, 79);
                canvas.DrawLine(69, 72, 72, 89);
                canvas.DrawLine(82, 79, 72, 89);
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
