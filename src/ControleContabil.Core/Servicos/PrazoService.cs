namespace ControleContabil.Core;

public static class PrazoService
{
    /// <summary>Calcula a situação de uma obrigação em relação ao prazo.</summary>
    /// <param name="diasAlerta">Quantos dias antes do vencimento ela passa a "próxima do vencimento".</param>
    public static SituacaoPrazo Situacao(StatusObrigacao status, DateOnly vencimento, DateOnly hoje, int diasAlerta)
    {
        if (status is StatusObrigacao.Concluida or StatusObrigacao.Dispensada)
            return SituacaoPrazo.Concluida;
        if (vencimento < hoje)
            return SituacaoPrazo.Atrasada;
        if (vencimento.DayNumber - hoje.DayNumber <= diasAlerta)
            return SituacaoPrazo.ProximoVencimento;
        return SituacaoPrazo.EmDia;
    }
}

public static class DataHora
{
    public static DateTime ParaLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
    public static DateOnly ParaDataLocal(DateTime utc) => DateOnly.FromDateTime(ParaLocal(utc));
    public static DateOnly Hoje() => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Início do dia (local) convertido para UTC — para filtrar colunas gravadas em UTC.</summary>
    public static DateTime InicioDoDiaUtc(DateOnly d) =>
        DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
}
