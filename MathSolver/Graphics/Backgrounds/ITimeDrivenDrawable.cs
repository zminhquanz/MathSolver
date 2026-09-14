using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

public interface ITimeDrivenDrawable : IDrawable
{
    double TimeSeconds { get; set; }
}
