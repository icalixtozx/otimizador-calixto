using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Optimizer.App.Converters;

public sealed class SignatureLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => "Assinado",
        false => "Sem assinatura",
        _ => "Desconhecido"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class SignatureBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        true => new SolidColorBrush(Color.FromRgb(239, 239, 237)),
        false => new SolidColorBrush(Color.FromRgb(232, 232, 230)),
        _ => new SolidColorBrush(Color.FromRgb(246, 246, 244))
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class SignatureForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool signed && !signed ? new SolidColorBrush(Color.FromRgb(68, 68, 68)) : new SolidColorBrush(Color.FromRgb(49, 49, 49));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ProblemCodeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        int code when code > 0 => $"Código {code}",
        int code when code == 0 => "Sem problema",
        _ => "Não informado"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ProblemForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int code && code > 0 ? new SolidColorBrush(Color.FromRgb(17, 17, 17)) : new SolidColorBrush(Color.FromRgb(132, 132, 132));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
