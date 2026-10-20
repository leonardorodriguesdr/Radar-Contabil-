using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

public class DocumentoLinha
{
    public int Id { get; set; }
    public Guid Versao { get; set; }
    public int EmpresaId { get; set; }
    public string Empresa { get; set; } = "";
    public string NomeArquivo { get; set; } = "";
    public string? Obrigacao { get; set; }
    public long TamanhoBytes { get; set; }
    public string EnviadoPor { get; set; } = "";
    public DateTime EnviadoEmUtc { get; set; }
    public string CaminhoRelativo { get; set; } = "";
    public string TamanhoTexto => TamanhoBytes switch
    {
        < 1024 => $"{TamanhoBytes} B",
        < 1024 * 1024 => $"{TamanhoBytes / 1024.0:0.#} KB",
        _ => $"{TamanhoBytes / 1024.0 / 1024.0:0.#} MB"
    };
}

public class DocumentoService
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly IArmazenamentoDocumentos _armazenamento;

    public DocumentoService(IDbContextFactory<AppDbContext> fabrica, IArmazenamentoDocumentos armazenamento)
    {
        _fabrica = fabrica;
        _armazenamento = armazenamento;
    }

    public async Task<List<DocumentoLinha>> ListarAsync(int? empresaId, string? texto, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var q = ctx.Documentos.AsNoTracking().Include(d => d.Empresa).Include(d => d.Obrigacao).AsQueryable();
        if (empresaId is { } emp) q = q.Where(d => d.EmpresaId == emp);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            q = q.Where(d => d.NomeOriginal.ToLower().Contains(t));
        }

        var docs = await q.OrderByDescending(d => d.CriadoEm).Take(2000).ToListAsync(ct);
        return docs.Select(d => new DocumentoLinha
        {
            Id = d.Id,
            Versao = d.Versao,
            EmpresaId = d.EmpresaId,
            Empresa = d.Empresa?.RazaoSocial ?? "",
            NomeArquivo = d.NomeOriginal,
            Obrigacao = d.Obrigacao is null ? null : $"{d.Obrigacao.Nome} (venc. {d.Obrigacao.Vencimento:dd/MM/yyyy})",
            TamanhoBytes = d.TamanhoBytes,
            EnviadoPor = d.EnviadoPorNome ?? "—",
            EnviadoEmUtc = d.CriadoEm,
            CaminhoRelativo = d.CaminhoRelativo
        }).ToList();
    }

    public async Task<int> EnviarAsync(int empresaId, int? obrigacaoId, IEnumerable<string> arquivos, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var empresa = await ctx.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == empresaId, ct)
                      ?? throw new ValidacaoException("Empresa não encontrada.");

        DateOnly competencia = DataHora.Hoje();
        if (obrigacaoId is { } oid)
        {
            var o = await ctx.Obrigacoes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == oid, ct)
                    ?? throw new ValidacaoException("Obrigação não encontrada.");
            if (o.EmpresaId != empresaId) throw new ValidacaoException("A obrigação não pertence à empresa selecionada.");
            competencia = o.Competencia;
        }

        var nomeUsuario = ctx.UsuarioAtualId is { } uid
            ? await ctx.Usuarios.Where(u => u.Id == uid).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        var enviados = new List<string>();
        try
        {
            foreach (var arquivo in arquivos)
            {
                var (relativo, hash, tamanho) = await _armazenamento.SalvarAsync(empresa.Cnpj, competencia, arquivo, ct);
                enviados.Add(relativo);
                ctx.Documentos.Add(new Documento
                {
                    EmpresaId = empresaId,
                    ObrigacaoId = obrigacaoId,
                    NomeOriginal = Path.GetFileName(arquivo),
                    CaminhoRelativo = relativo,
                    TamanhoBytes = tamanho,
                    HashSha256 = hash,
                    EnviadoPorId = ctx.UsuarioAtualId,
                    EnviadoPorNome = nomeUsuario
                });
            }
            await Persistencia.SalvarAsync(ctx, ct);
        }
        catch
        {
            // Não deixa arquivos órfãos no repositório se o banco falhar.
            foreach (var r in enviados) await _armazenamento.RemoverAsync(r, CancellationToken.None);
            throw;
        }

        return enviados.Count;
    }

    public async Task RemoverAsync(int id, Guid versao, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var d = await ctx.Documentos.FindAsync(new object[] { id }, ct) ?? throw new ValidacaoException("Documento não encontrado.");
        if (d.Versao != versao) throw new ConcorrenciaException();

        var caminho = d.CaminhoRelativo;
        ctx.Documentos.Remove(d);
        await Persistencia.SalvarAsync(ctx, ct);
        await _armazenamento.RemoverAsync(caminho, ct);
    }

    public string CaminhoCompleto(string caminhoRelativo) => _armazenamento.CaminhoCompleto(caminhoRelativo);
    public bool Existe(string caminhoRelativo) => _armazenamento.Existe(caminhoRelativo);
}
