using ControleContabil.Core;

namespace ControleContabil.Infrastructure;

public class BancoOptions
{
    /// <summary>"SqlServer" ou "PostgreSql".</summary>
    public string Provedor { get; set; } = "SqlServer";
    public string ConnectionString { get; set; } = "";
    public bool EhPostgres => string.Equals(Provedor, "PostgreSql", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Provedor, "Postgres", StringComparison.OrdinalIgnoreCase);
}

public class ArmazenamentoOptions
{
    /// <summary>Pasta raiz dos documentos. Pode ser caminho UNC (\\servidor\contabil\docs) ou uma pasta
    /// sincronizada com a nuvem (OneDrive, Google Drive, Dropbox).</summary>
    public string RaizDocumentos { get; set; } = "";
}

public class AlertaOptions
{
    /// <summary>Dias de antecedência para a obrigação ser marcada como "próxima do vencimento".</summary>
    public int DiasAlerta { get; set; } = 5;
}

/// <summary>Usuário logado no app. Singleton lido pelo AppDbContext para gravar a auditoria.</summary>
public class UsuarioAtual
{
    public int? Id { get; set; }
    public string Nome { get; set; } = "Sistema";
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Analista;
    public bool PodeAdministrar => Perfil >= PerfilUsuario.Coordenador;
}
