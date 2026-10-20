using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ControleContabil.Core;

namespace ControleContabil.Desktop.Infra;

/// <summary>Qualquer enum do domínio → texto em português.</summary>
public class EnumRotuloConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Rotulos.Para(value);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public class CnpjConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        CnpjValidator.Formatar(value as string);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>DateTime gravado em UTC → data/hora local formatada.</summary>
public class UtcLocalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return "";
        var formato = parameter as string ?? "dd/MM/yyyy HH:mm";
        return DataHora.ParaLocal(dt).ToString(formato, culture);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Situação do prazo → cor do "chip" de status.</summary>
public class SituacaoBrushConverter : IValueConverter
{
    private static readonly Brush EmDia = Criar("#2E7D32");
    private static readonly Brush Proximo = Criar("#EF6C00");
    private static readonly Brush Atrasada = Criar("#C62828");
    private static readonly Brush Finalizada = Criar("#546E7A");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        SituacaoPrazo.EmDia => EmDia,
        SituacaoPrazo.ProximoVencimento => Proximo,
        SituacaoPrazo.Atrasada => Atrasada,
        SituacaoPrazo.Concluida => Finalizada,
        _ => Finalizada
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    private static Brush Criar(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }
}
