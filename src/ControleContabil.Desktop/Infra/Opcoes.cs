using System.Globalization;
using ControleContabil.Core;

namespace ControleContabil.Desktop.Infra;

public record Opcao<T>(T Valor, string Texto);

public record MesItem(DateOnly Valor, string Texto);

/// <summary>Listas prontas para ComboBox.</summary>
public static class Opcoes
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<Opcao<RegimeTributario>> Regimes { get; } = new[]
    {
        new Opcao<RegimeTributario>(RegimeTributario.SimplesNacional, "Simples Nacional"),
        new Opcao<RegimeTributario>(RegimeTributario.LucroPresumido, "Lucro Presumido"),
        new Opcao<RegimeTributario>(RegimeTributario.LucroReal, "Lucro Real")
    };

    public static IReadOnlyList<Opcao<TipoObrigacao>> Tipos { get; } =
        Enum.GetValues<TipoObrigacao>().Select(v => new Opcao<TipoObrigacao>(v, Rotulos.Tipo(v))).ToList();

    public static IReadOnlyList<Opcao<Periodicidade>> Periodicidades { get; } =
        Enum.GetValues<Periodicidade>().Select(v => new Opcao<Periodicidade>(v, Rotulos.Periodicidade(v))).ToList();

    public static IReadOnlyList<Opcao<RegraVencimento>> Regras { get; } =
        Enum.GetValues<RegraVencimento>().Select(v => new Opcao<RegraVencimento>(v, Rotulos.Regra(v))).ToList();

    public static IReadOnlyList<Opcao<PerfilUsuario>> Perfis { get; } =
        Enum.GetValues<PerfilUsuario>().Select(v => new Opcao<PerfilUsuario>(v, Rotulos.Perfil(v))).ToList();

    public static IReadOnlyList<Opcao<SituacaoPrazo?>> Situacoes { get; } = new[]
    {
        new Opcao<SituacaoPrazo?>(null, "Todas as situações"),
        new Opcao<SituacaoPrazo?>(SituacaoPrazo.EmDia, "Em dia"),
        new Opcao<SituacaoPrazo?>(SituacaoPrazo.ProximoVencimento, "Próximas do vencimento"),
        new Opcao<SituacaoPrazo?>(SituacaoPrazo.Atrasada, "Atrasadas"),
        new Opcao<SituacaoPrazo?>(SituacaoPrazo.Concluida, "Finalizadas")
    };

    /// <summary>Meses de (hoje − mesesAtras) até (hoje + mesesAFrente), do mais recente ao mais antigo.</summary>
    public static List<MesItem> Meses(int mesesAtras, int mesesAFrente)
    {
        var inicio = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var lista = new List<MesItem>();
        for (var i = mesesAFrente; i >= -mesesAtras; i--)
        {
            var d = inicio.AddMonths(i);
            lista.Add(new MesItem(d, Capitalizar(d.ToString("MMMM/yyyy", PtBr))));
        }
        return lista;
    }

    public static string Capitalizar(string s) => s.Length == 0 ? s : char.ToUpper(s[0], PtBr) + s[1..];
}
