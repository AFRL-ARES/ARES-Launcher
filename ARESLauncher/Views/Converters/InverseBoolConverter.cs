using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ARESLauncher.Views.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
  public static InverseBoolConverter Instance { get; } = new();

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    return value is bool b ? !b : value;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
  {
    return value is bool b ? !b : value;
  }
}
