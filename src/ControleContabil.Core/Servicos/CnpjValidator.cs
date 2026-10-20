namespace ControleContabil.Core;

/// <summary>Validação/formatação de CNPJ, incluindo o formato alfanumérico (a partir de jul/2026).
/// Para cada caractere o valor é (código ASCII − 48); dígitos numéricos mantêm o valor de sempre.</summary>
public static class CnpjValidator
{
    public static string Normalizar(string? valor) =>
        new string((valor ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

    public static bool EhValido(string? valor)
    {
        var c = Normalizar(valor);
        if (c.Length != 14) return false;
        if (!char.IsAsciiDigit(c[12]) || !char.IsAsciiDigit(c[13])) return false;
        if (c.Distinct().Count() == 1) return false;

        var d1 = CalcularDigito(c.AsSpan(0, 12));
        var d2 = CalcularDigito(c.AsSpan(0, 13));
        return c[12] - '0' == d1 && c[13] - '0' == d2;
    }

    public static string Formatar(string? valor)
    {
        var c = Normalizar(valor);
        return c.Length == 14
            ? $"{c[..2]}.{c[2..5]}.{c[5..8]}/{c[8..12]}-{c[12..]}"
            : valor ?? "";
    }

    private static int CalcularDigito(ReadOnlySpan<char> baseCnpj)
    {
        var soma = 0;
        var peso = 2;
        for (var i = baseCnpj.Length - 1; i >= 0; i--)
        {
            soma += (baseCnpj[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
