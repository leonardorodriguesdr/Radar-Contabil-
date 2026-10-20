namespace ControleContabil.Core;

/// <summary>Regras puras (sem banco) para decidir se um modelo gera obrigação e calcular vencimentos.</summary>
public static class ObrigacaoGerador
{
    /// <param name="competencia">Primeiro dia do mês de competência sendo fechado.</param>
    public static bool DeveGerar(ObrigacaoModelo modelo, Empresa empresa, DateOnly competencia)
    {
        if (!modelo.Ativo || !empresa.Ativa) return false;
        if ((modelo.RegimesAplicaveis & empresa.Regime) == 0) return false;

        return modelo.Periodicidade switch
        {
            Periodicidade.Mensal => true,
            Periodicidade.Trimestral => competencia.Month % 3 == 0,
            Periodicidade.Anual => competencia.Month == 12,
            _ => false
        };
    }

    public static DateOnly CalcularVencimento(ObrigacaoModelo modelo, DateOnly competencia)
    {
        int ano, mes;
        if (modelo.Periodicidade == Periodicidade.Anual)
        {
            ano = competencia.Year + 1;
            mes = Math.Clamp(modelo.MesVencimento, 1, 12);
        }
        else
        {
            var alvo = new DateOnly(competencia.Year, competencia.Month, 1).AddMonths(Math.Max(0, modelo.MesesAposCompetencia));
            ano = alvo.Year;
            mes = alvo.Month;
        }

        return modelo.Regra switch
        {
            RegraVencimento.NesimoDiaUtil => DiasUteis.NesimoDiaUtil(ano, mes, modelo.Dia),
            RegraVencimento.UltimoDiaUtil => DiasUteis.UltimoDiaUtil(ano, mes),
            _ => DiasUteis.Ajustar(
                    new DateOnly(ano, mes, Math.Clamp(modelo.Dia, 1, DateTime.DaysInMonth(ano, mes))),
                    modelo.AntecipaDiaNaoUtil)
        };
    }

    public static Obrigacao Criar(ObrigacaoModelo modelo, Empresa empresa, DateOnly competencia) => new()
    {
        EmpresaId = empresa.Id,
        ModeloId = modelo.Id,
        Nome = modelo.Nome,
        Tipo = modelo.Tipo,
        Periodicidade = modelo.Periodicidade,
        Competencia = new DateOnly(competencia.Year, competencia.Month, 1),
        Vencimento = CalcularVencimento(modelo, competencia),
        Status = StatusObrigacao.Pendente,
        ResponsavelId = empresa.AnalistaId
    };

    public static bool ChecklistAplica(ChecklistModelo modelo, Empresa empresa)
    {
        if (!modelo.Ativo || !empresa.Ativa) return false;
        return modelo.EmpresaId is { } id
            ? id == empresa.Id
            : (modelo.RegimesAplicaveis & empresa.Regime) != 0;
    }
}
