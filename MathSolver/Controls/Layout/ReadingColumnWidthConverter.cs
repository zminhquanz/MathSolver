using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Controls;

/// <summary>Constrain a reading card from its viewport before native text measurement.</summary>
internal sealed class ReadingColumnWidthConverter(double maximum, double inset, bool capBeforeInset = false) : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ReadingColumnPolicy.Width(value is double width ? width : double.NaN, maximum, inset, capBeforeInset);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
