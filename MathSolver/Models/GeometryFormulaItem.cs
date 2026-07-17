using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MathSolver.Models;

public sealed class GeometryFormulaItem :
    INotifyPropertyChanged
{
    private double _cardHeight = 570;

    public string Name { get; set; } = string.Empty;

    public IDrawable? Diagram { get; set; }

    public ObservableCollection<string> Formulas { get; } = [];

    public ObservableCollection<string> Symbols { get; } = [];

    public double CardHeight
    {
        get => _cardHeight;

        set
        {
            if (Math.Abs(_cardHeight - value) < 0.1)
            {
                return;
            }

            _cardHeight = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}