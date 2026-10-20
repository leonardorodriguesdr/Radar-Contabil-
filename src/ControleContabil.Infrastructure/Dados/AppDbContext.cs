using System.Globalization;
using System.Text;
using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ControleContabil.Infrastructure;

public class AppDbContext : DbContext
{
    private readonly UsuarioAtual _usuario;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly HashSet<string> PropriedadesIgnoradas =
        new() { nameof(EntidadeBase.Versao), nameof(EntidadeBase.AtualizadoEm), nameof(EntidadeBase.CriadoEm) };

    public AppDbContext(DbContextOptions<AppDbContext> options, UsuarioAtual usuario) : base(options)
    {
        _usuario = usuario;
    }

    /// <summary>Desliga a auditoria automática (cargas em lote registram uma única linha-resumo).</summary>
    public bool AuditoriaDesativada { get; set; }

    public int? UsuarioAtualId => _usuario.Id;

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<ObrigacaoModelo> ObrigacaoModelos => Set<ObrigacaoModelo>();
    public DbSet<Obrigacao> Obrigacoes => Set<Obrigacao>();
    public DbSet<ChecklistModelo> ChecklistModelos => Set<ChecklistModelo>();
    public DbSet<ChecklistModeloItem> ChecklistModeloItens => Set<ChecklistModeloItem>();
    public DbSet<ChecklistExecucao> ChecklistExecucoes => Set<ChecklistExecucao>();
    public DbSet<ChecklistExecucaoItem> ChecklistExecucaoItens => Set<ChecklistExecucaoItem>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<AuditoriaLog> AuditoriaLogs => Set<AuditoriaLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.LoginWindows).HasMaxLength(120);
            e.HasIndex(x => x.LoginWindows);
        });

        b.Entity<Empresa>(e =>
        {
            e.Property(x => x.Cnpj).HasMaxLength(14).IsRequired();
            e.HasIndex(x => x.Cnpj).IsUnique();
            e.Property(x => x.RazaoSocial).HasMaxLength(200).IsRequired();
            e.Property(x => x.NomeFantasia).HasMaxLength(200);
            e.Property(x => x.Observacoes).HasMaxLength(2000);
            e.HasOne(x => x.Analista).WithMany().HasForeignKey(x => x.AnalistaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AnalistaBackup).WithMany().HasForeignKey(x => x.AnalistaBackupId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ObrigacaoModelo>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(500);
        });

        b.Entity<Obrigacao>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Observacoes).HasMaxLength(2000);
            e.HasIndex(x => x.Vencimento);
            e.HasIndex(x => new { x.EmpresaId, x.Competencia });
            e.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Modelo).WithMany().HasForeignKey(x => x.ModeloId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Responsavel).WithMany().HasForeignKey(x => x.ResponsavelId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ChecklistModelo>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.ModeloId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChecklistModeloItem>(e => e.Property(x => x.Descricao).HasMaxLength(300).IsRequired());

        b.Entity<ChecklistExecucao>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.EmpresaId, x.Competencia });
            e.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.ExecucaoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChecklistExecucaoItem>(e =>
        {
            e.Property(x => x.Descricao).HasMaxLength(300).IsRequired();
            e.Property(x => x.ConcluidoPorNome).HasMaxLength(120);
        });

        b.Entity<Documento>(e =>
        {
            e.Property(x => x.NomeOriginal).HasMaxLength(260).IsRequired();
            e.Property(x => x.CaminhoRelativo).HasMaxLength(500).IsRequired();
            e.Property(x => x.HashSha256).HasMaxLength(64);
            e.Property(x => x.EnviadoPorNome).HasMaxLength(120);
            e.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Obrigacao).WithMany().HasForeignKey(x => x.ObrigacaoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditoriaLog>(e =>
        {
            e.Property(x => x.UsuarioNome).HasMaxLength(120);
            e.Property(x => x.Entidade).HasMaxLength(60);
            e.Property(x => x.EntidadeId).HasMaxLength(40);
            e.Property(x => x.Acao).HasMaxLength(20);
            e.Property(x => x.Descricao).HasMaxLength(400);
            e.HasIndex(x => x.DataHoraUtc);
            e.HasIndex(x => x.EmpresaId);
        });

        // Concorrência otimista em todas as entidades (provider-agnóstico).
        foreach (var tipo in b.Model.GetEntityTypes().Where(t => typeof(EntidadeBase).IsAssignableFrom(t.ClrType)))
            b.Entity(tipo.ClrType).Property(nameof(EntidadeBase.Versao)).IsConcurrencyToken();
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var agora = DateTime.UtcNow;
        var pendentes = new List<(AuditoriaLog Log, EntityEntry? Entrada)>();

        foreach (var entrada in ChangeTracker.Entries().ToList())
        {
            if (entrada.Entity is EntidadeBase eb && entrada.State is EntityState.Added or EntityState.Modified)
            {
                eb.Versao = Guid.NewGuid();
                if (entrada.State == EntityState.Added) eb.CriadoEm = agora;
                eb.AtualizadoEm = agora;
            }

            if (AuditoriaDesativada || !Auditado(entrada.Entity)) continue;

            string acao;
            string? detalhes = null;
            switch (entrada.State)
            {
                case EntityState.Added: acao = "Criado"; break;
                case EntityState.Deleted: acao = "Excluído"; break;
                case EntityState.Modified:
                    acao = "Alterado";
                    detalhes = Diferencas(entrada);
                    if (string.IsNullOrEmpty(detalhes)) continue;
                    break;
                default: continue;
            }

            var log = NovoLog(entrada.Entity.GetType().Name, acao, Descrever(entrada.Entity), EmpresaDe(entrada.Entity), detalhes);
            if (entrada.State != EntityState.Added)
                log.EntidadeId = entrada.Property(nameof(EntidadeBase.Id)).CurrentValue?.ToString();
            pendentes.Add((log, entrada.State == EntityState.Added ? entrada : null));
        }

        var abriuTransacao = Database.CurrentTransaction is null && pendentes.Count > 0;
        var tx = abriuTransacao ? await Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var resultado = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            if (pendentes.Count > 0)
            {
                foreach (var (log, entrada) in pendentes)
                {
                    if (entrada is not null)
                        log.EntidadeId = entrada.Property(nameof(EntidadeBase.Id)).CurrentValue?.ToString();
                    // O Id de uma empresa recém-criada só existe agora.
                    if (entrada?.Entity is Empresa nova) log.EmpresaId = nova.Id;
                }
                AuditoriaLogs.AddRange(pendentes.Select(p => p.Log));
                await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            if (tx is not null) await tx.CommitAsync(cancellationToken);
            return resultado;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    /// <summary>Cria uma linha de auditoria manual (ex.: resumo de uma geração em lote). Chame SaveChanges depois.</summary>
    public AuditoriaLog NovoLog(string entidade, string acao, string descricao, int? empresaId = null, string? detalhes = null)
    {
        var log = new AuditoriaLog
        {
            DataHoraUtc = DateTime.UtcNow,
            UsuarioId = _usuario.Id,
            UsuarioNome = _usuario.Nome,
            Entidade = entidade,
            Acao = acao,
            Descricao = Truncar(descricao, 400),
            EmpresaId = empresaId,
            Detalhes = detalhes
        };
        return log;
    }

    public void RegistrarManual(string entidade, string acao, string descricao, int? empresaId = null, string? detalhes = null)
        => AuditoriaLogs.Add(NovoLog(entidade, acao, descricao, empresaId, detalhes));

    private static bool Auditado(object e) =>
        e is Empresa or Usuario or Obrigacao or ObrigacaoModelo or ChecklistModelo
            or ChecklistExecucao or ChecklistExecucaoItem or Documento;

    private static int? EmpresaDe(object e) => e switch
    {
        Empresa x => x.Id == 0 ? null : x.Id,
        Obrigacao x => x.EmpresaId,
        Documento x => x.EmpresaId,
        ChecklistExecucao x => x.EmpresaId,
        _ => null
    };

    private static string Descrever(object e) => e switch
    {
        Empresa x => $"{x.RazaoSocial} ({CnpjValidator.Formatar(x.Cnpj)})",
        Usuario x => x.Nome,
        Obrigacao x => $"{x.Nome} — venc. {x.Vencimento:dd/MM/yyyy}",
        ObrigacaoModelo x => x.Nome,
        ChecklistModelo x => x.Nome,
        ChecklistExecucao x => $"{x.Nome} — {x.Competencia:MM/yyyy}",
        ChecklistExecucaoItem x => x.Descricao,
        Documento x => x.NomeOriginal,
        _ => e.GetType().Name
    };

    private static string Diferencas(EntityEntry entrada)
    {
        var sb = new StringBuilder();
        foreach (var p in entrada.Properties)
        {
            if (!p.IsModified || PropriedadesIgnoradas.Contains(p.Metadata.Name)) continue;
            if (Equals(p.OriginalValue, p.CurrentValue)) continue;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(p.Metadata.Name).Append(": ").Append(Fmt(p.OriginalValue)).Append(" → ").Append(Fmt(p.CurrentValue));
        }
        return sb.ToString();
    }

    private static string Fmt(object? v) => v switch
    {
        null => "—",
        DateOnly d => d.ToString("dd/MM/yyyy", PtBr),
        DateTime dt => DataHora.ParaLocal(dt).ToString("dd/MM/yyyy HH:mm", PtBr),
        bool bo => bo ? "sim" : "não",
        IFormattable f => f.ToString(null, PtBr),
        _ => v.ToString() ?? "—"
    };

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

public sealed class AppDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _opcoes;
    private readonly UsuarioAtual _usuario;

    public AppDbContextFactory(BancoOptions banco, UsuarioAtual usuario)
    {
        if (string.IsNullOrWhiteSpace(banco.ConnectionString))
            throw new InvalidOperationException("Configure a string de conexão em appsettings.json (seção \"Banco\").");

        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (banco.EhPostgres) builder.UseNpgsql(banco.ConnectionString);
        else builder.UseSqlServer(banco.ConnectionString);
        _opcoes = builder.Options;
        _usuario = usuario;
    }

    public AppDbContext CreateDbContext() => new(_opcoes, _usuario);
}
