using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sublingual.App.Converters;

/// <summary>
/// Returns true when the bound string equals ConverterParameter.
/// Used for tab visibility: IsVisible="{Binding ActiveTab, Converter=..., ConverterParameter=capture}"
/// </summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
