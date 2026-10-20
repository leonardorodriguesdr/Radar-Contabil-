namespace ControleContabil.Core;

/// <summary>Regime tributário. É [Flags] para que modelos possam se aplicar a mais de um regime.
/// Uma empresa tem exatamente um valor (1, 2 ou 4).</summary>
[Flags]
public enum RegimeTributario
{
    Nenhum = 0,
    SimplesNacional = 1,
    LucroPresumido = 2,
    LucroReal = 4,
    Todos = SimplesNacional | LucroPresumido | LucroReal
}

public enum PerfilUsuario { Analista = 0, Coordenador = 1, Administrador = 2 }

public enum TipoObrigacao
{
    Conciliacao = 0,
    Balancete = 1,
    DRE = 2,
    ApuracaoImposto = 3,
    ObrigacaoAcessoria = 4,
    Fechamento = 5,
    Outro = 6
}

public enum Periodicidade { Mensal = 0, Trimestral = 1, Anual = 2 }

public enum RegraVencimento
{
    /// <summary>Dia fixo do mês (ajustado se cair em dia não útil).</summary>
    DiaFixo = 0,
    /// <summary>N-ésimo dia útil do mês.</summary>
    NesimoDiaUtil = 1,
    /// <summary>Último dia útil do mês.</summary>
    UltimoDiaUtil = 2
}

public enum StatusObrigacao { Pendente = 0, EmAndamento = 1, Concluida = 2, Dispensada = 3 }

/// <summary>Situação calculada em relação ao prazo (não é persistida).</summary>
public enum SituacaoPrazo { EmDia = 0, ProximoVencimento = 1, Atrasada = 2, Concluida = 3 }
