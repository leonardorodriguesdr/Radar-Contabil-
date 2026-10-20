using System.Globalization;
using System.IO;
using System.Text;

namespace ControleContabil.Desktop.Infra;

/// <summary>Exporta listas para CSV no padrão que o Excel brasileiro abre direto (separador ";" e UTF-8 com BOM).</summary>
public static class CsvExport
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static void Salvar<T>(string caminho, IEnumerable<T> linhas, params (string Titulo, Func<T, object?> Valor)[] colunas)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", colunas.Select(c => Escapar(c.Titulo))));
        foreach (var linha in linhas)
            sb.AppendLine(string.Join(";", colunas.Select(c => Escapar(Formatar(c.Valor(linha))))));
        File.WriteAllText(caminho, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Formatar(object? v) => v switch
    {
        null => "",
        DateOnly d => d.ToString("dd/MM/yyyy", PtBr),
        DateTime dt => dt.ToString("dd/MM/yyyy HH:mm", PtBr),
        bool b => b ? "Sim" : "Não",
        IFormattable f => f.ToString(null, PtBr),
        _ => v.ToString() ?? ""
    };

    private static string Escapar(string s)
    {
        // Neutraliza "injeção de fórmula" ao abrir no Excel.
        if (s.Length > 0 && s[0] is '=' or '+' or '@' || (s.Length > 1 && s[0] == '-' && !char.IsDigit(s[1])))
            s = "'" + s;

        return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }
}
