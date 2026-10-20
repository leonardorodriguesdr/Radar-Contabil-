using System.Globalization;

namespace ControleContabil.Core;

/// <summary>Linha de obrigação já com empresa, responsável e situação calculada (para grids e dashboard).</summary>
public class ObrigacaoItem
{
    public int Id { get; set; }
    public Guid Versao { get; set; }
    public int EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = "";
    public string EmpresaCnpj { get; set; } = "";
    public RegimeTributario Regime { get; set; }
    public int? ResponsavelId { get; set; }
    public string ResponsavelNome { get; set; } = "—";
    public string Nome { get; set; } = "";
    public TipoObrigacao Tipo { get; set; }
    public Periodicidade Periodicidade { get; set; }
    public DateOnly Competencia { get; set; }
    public DateOnly Vencimento { get; set; }
    public StatusObrigacao Status { get; set; }
    public SituacaoPrazo Situacao { get; set; }
    public int DiasParaVencer { get; set; }
    public DateTime? ConcluidaEm { get; set; }
    public string? Observacoes { get; set; }

    public bool Aberta => Status is StatusObrigacao.Pendente or StatusObrigacao.EmAndamento;
    public string StatusTexto => Rotulos.Status(Status);
    public string SituacaoTexto => Rotulos.Situacao(Situacao);
    public string RegimeTexto => Rotulos.Regime(Regime);
    public string TipoTexto => Rotulos.Tipo(Tipo);

    public string CompetenciaTexto => Periodicidade switch
    {
        Periodicidade.Anual => $"Exercício {Competencia.Year}",
        Periodicidade.Trimestral => $"{Competencia.Month / 3}º tri/{Competencia.Year}",
        _ => Competencia.ToString("MM/yyyy", CultureInfo.InvariantCulture)
    };

    public string PrazoTexto
    {
        get
        {
            if (Status == StatusObrigacao.Dispensada) return "Dispensada";
            if (Status == StatusObrigacao.Concluida)
                return ConcluidaEm is { } c
                    ? $"Concluída em {DataHora.ParaLocal(c):dd/MM/yyyy}"
                    : "Concluída";
            if (DiasParaVencer < 0) return $"Venceu há {-DiasParaVencer} {(DiasParaVencer == -1 ? "dia" : "dias")}";
            if (DiasParaVencer == 0) return "Vence hoje";
            return $"Vence em {DiasParaVencer} {(DiasParaVencer == 1 ? "dia" : "dias")}";
        }
    }
}

public class ObrigacaoFiltro
{
    public DateOnly? VencimentoDe { get; set; }
    public DateOnly? VencimentoAte { get; set; }

    /// <summary>Quando há intervalo de vencimento, também traz as ainda abertas que venceram antes dele.</summary>
    public bool IncluirAtrasadasAnteriores { get; set; }

    public int? EmpresaId { get; set; }
    public int? ResponsavelId { get; set; }
    public SituacaoPrazo? Situacao { get; set; }
    public bool OcultarFinalizadas { get; set; }
    public string? Texto { get; set; }
}

/// <summary>Dados do formulário de obrigação (criação avulsa ou edição).</summary>
public class ObrigacaoEdicao
{
    public int Id { get; set; }
    public Guid Versao { get; set; }
    public int EmpresaId { get; set; }
    public string Nome { get; set; } = "";
    public TipoObrigacao Tipo { get; set; } = TipoObrigacao.Outro;
    public DateOnly Competencia { get; set; } = DataHora.Hoje();
    public DateOnly Vencimento { get; set; } = DataHora.Hoje();
    public int? ResponsavelId { get; set; }
    public string? Observacoes { get; set; }
}

public class ChecklistResumo
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = "";
    public string AnalistaNome { get; set; } = "—";
    public string Nome { get; set; } = "";
    public DateOnly Competencia { get; set; }
    public int Total { get; set; }
    public int Concluidos { get; set; }
    public double Percentual => Total == 0 ? 0 : Math.Round(100.0 * Concluidos / Total, 0);
    public string ProgressoTexto => $"{Concluidos}/{Total}";
}

public class ProdutividadeLinha
{
    public string Analista { get; set; } = "";
    public int Total { get; set; }
    public int Concluidas { get; set; }
    public int ConcluidasNoPrazo { get; set; }
    public int ConcluidasComAtraso { get; set; }
    public int EmAberto { get; set; }
    public int Atrasadas { get; set; }
    public int ItensChecklistConcluidos { get; set; }
    public double PercConclusao => Total == 0 ? 0 : Math.Round(100.0 * Concluidas / Total, 1);
    public double PercNoPrazo => Concluidas == 0 ? 0 : Math.Round(100.0 * ConcluidasNoPrazo / Concluidas, 1);
}

public class PendenciaClienteLinha
{
    public int EmpresaId { get; set; }
    public string Empresa { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public RegimeTributario Regime { get; set; }
    public string Analista { get; set; } = "—";
    public int Abertas { get; set; }
    public int Atrasadas { get; set; }
    public int Proximas { get; set; }
    public DateOnly? ProximoVencimento { get; set; }
    public string RegimeTexto => Rotulos.Regime(Regime);
}

public class AuditoriaFiltro
{
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public int? UsuarioId { get; set; }
    public string? Texto { get; set; }
    public int Limite { get; set; } = 1000;
}

public class AuditoriaLinha
{
    public DateTime DataHoraLocal { get; set; }
    public string Usuario { get; set; } = "";
    public string Acao { get; set; } = "";
    public string Entidade { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string? Detalhes { get; set; }
    public string Empresa { get; set; } = "";
}
