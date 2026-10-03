using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private sealed class ContextRotation
    {
        private int[] _order = [];
        private int _position;
        private int _last = -1;

        public int Next(Random random, int count)
        {
            if (_position == _order.Length)
            {
                _order = Enumerable.Range(0, count).ToArray();
                random.Shuffle(_order);
                // A new shuffled cycle must not repeat the preceding context.
                if (count > 1 && _order[0] == _last)
                    (_order[0], _order[1]) = (_order[1], _order[0]);
                _position = 0;
            }
            return _last = _order[_position++];
        }
    }

    private readonly Dictionary<(ElementaryQuizType, AppLanguage, string), ContextRotation> _contextRotations = [];

    private int NextContextVariant(ElementaryQuizType type, AppLanguage language, string dimension, int count)
    {
        var key = (type, language, dimension);
        if (!_contextRotations.TryGetValue(key, out var rotation))
            _contextRotations[key] = rotation = new();
        return rotation.Next(_random, count);
    }

}
