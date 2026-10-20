namespace ControleContabil.Core;

public abstract class EntidadeBase
{
    public int Id { get; set; }

    /// <summary>Token de concorrência otimista (trocado a cada gravação). Independe do provedor de banco.</summary>
    public Guid Versao { get; set; } = Guid.NewGuid();

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

public class Usuario : EntidadeBase
{
    public string Nome { get; set; } = "";
    public string? Email { get; set; }

    /// <summary>Login do Windows (Environment.UserName) usado para identificar o usuário automaticamente.</summary>
    public string? LoginWindows { get; set; }

    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Analista;
    public bool Ativo { get; set; } = true;
}

public class Empresa : EntidadeBase
{
    /// <summary>CNPJ normalizado (14 caracteres, sem máscara; aceita o novo formato alfanumérico).</summary>
    public string Cnpj { get; set; } = "";

    public string RazaoSocial { get; set; } = "";
    public string? NomeFantasia { get; set; }
    public RegimeTributario Regime { get; set; } = RegimeTributario.SimplesNacional;
    public bool Ativa { get; set; } = true;
    public string? Observacoes { get; set; }

    public int? AnalistaId { get; set; }
    public Usuario? Analista { get; set; }

    public int? AnalistaBackupId { get; set; }
    public Usuario? AnalistaBackup { get; set; }
}

/// <summary>Modelo (catálogo) de obrigação: define regra de geração e de vencimento.</summary>
public class ObrigacaoModelo : EntidadeBase
{
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public TipoObrigacao Tipo { get; set; } = TipoObrigacao.ObrigacaoAcessoria;
    public Periodicidade Periodicidade { get; set; } = Periodicidade.Mensal;
    public RegraVencimento Regra { get; set; } = RegraVencimento.DiaFixo;

    /// <summary>Dia do mês (DiaFixo) ou ordem do dia útil (NesimoDiaUtil). Ignorado em UltimoDiaUtil.</summary>
    public int Dia { get; set; } = 20;

    /// <summary>Mensal/Trimestral: quantos meses depois do fim da competência vence (1 = mês seguinte).</summary>
    public int MesesAposCompetencia { get; set; } = 1;

    /// <summary>Anual: mês (1–12) do ano seguinte ao exercício em que vence.</summary>
    public int MesVencimento { get; set; } = 1;

    /// <summary>Se o vencimento cair em dia não útil: true = antecipa (mais seguro); false = prorroga.</summary>
    public bool AntecipaDiaNaoUtil { get; set; } = true;

    public RegimeTributario RegimesAplicaveis { get; set; } = RegimeTributario.Todos;
    public bool Ativo { get; set; } = true;
}

public class Obrigacao : EntidadeBase
{
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public int? ModeloId { get; set; }
    public ObrigacaoModelo? Modelo { get; set; }

    public string Nome { get; set; } = "";
    public TipoObrigacao Tipo { get; set; }
    public Periodicidade Periodicidade { get; set; }

    /// <summary>Primeiro dia do mês de competência (anual: dezembro do exercício).</summary>
    public DateOnly Competencia { get; set; }

    public DateOnly Vencimento { get; set; }
    public StatusObrigacao Status { get; set; } = StatusObrigacao.Pendente;

    public int? ResponsavelId { get; set; }
    public Usuario? Responsavel { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? ConcluidaEm { get; set; }
    public int? ConcluidaPorId { get; set; }
    public string? Observacoes { get; set; }
}

public class ChecklistModelo : EntidadeBase
{
    public string Nome { get; set; } = "";

    /// <summary>Quando EmpresaId é nulo, aplica-se às empresas dos regimes indicados.</summary>
    public RegimeTributario RegimesAplicaveis { get; set; } = RegimeTributario.Todos;

    /// <summary>Quando preenchido, o checklist vale só para esta empresa (ignora regimes).</summary>
    public int? EmpresaId { get; set; }

    public bool Ativo { get; set; } = true;
    public List<ChecklistModeloItem> Itens { get; set; } = new();
}

public class ChecklistModeloItem : EntidadeBase
{
    public int ModeloId { get; set; }
    public int Ordem { get; set; }
    public string Descricao { get; set; } = "";
    public bool Obrigatorio { get; set; } = true;
}

/// <summary>Instância de um checklist para uma empresa em uma competência (cópia dos itens do modelo).</summary>
public class ChecklistExecucao : EntidadeBase
{
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public int? ModeloId { get; set; }
    public string Nome { get; set; } = "";
    public DateOnly Competencia { get; set; }
    public List<ChecklistExecucaoItem> Itens { get; set; } = new();
}

public class ChecklistExecucaoItem : EntidadeBase
{
    public int ExecucaoId { get; set; }
    public int Ordem { get; set; }
    public string Descricao { get; set; } = "";
    public bool Obrigatorio { get; set; } = true;
    public bool Concluido { get; set; }
    public DateTime? ConcluidoEm { get; set; }
    public int? ConcluidoPorId { get; set; }
    public string? ConcluidoPorNome { get; set; }
}

public class Documento : EntidadeBase
{
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public int? ObrigacaoId { get; set; }
    public Obrigacao? Obrigacao { get; set; }

    public string NomeOriginal { get; set; } = "";

    /// <summary>Caminho relativo à raiz de armazenamento configurada.</summary>
    public string CaminhoRelativo { get; set; } = "";

    public long TamanhoBytes { get; set; }
    public string HashSha256 { get; set; } = "";
    public int? EnviadoPorId { get; set; }
    public string? EnviadoPorNome { get; set; }
}

public class AuditoriaLog
{
    public long Id { get; set; }
    public DateTime DataHoraUtc { get; set; }
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = "";
    public string Entidade { get; set; } = "";
    public string? EntidadeId { get; set; }
    public int? EmpresaId { get; set; }
    public string Acao { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string? Detalhes { get; set; }
}
