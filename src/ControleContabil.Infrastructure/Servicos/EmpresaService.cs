using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

public class EmpresaService
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;

    public EmpresaService(IDbContextFactory<AppDbContext> fabrica) => _fabrica = fabrica;

    public async Task<List<Empresa>> ListarAsync(string? busca = null, bool incluirInativas = false, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var q = ctx.Empresas.AsNoTracking().Include(e => e.Analista).Include(e => e.AnalistaBackup).AsQueryable();
        if (!incluirInativas) q = q.Where(e => e.Ativa);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim().ToLower();
            var cnpj = CnpjValidator.Normalizar(busca);
            q = q.Where(e => e.RazaoSocial.ToLower().Contains(b)
                             || (e.NomeFantasia != null && e.NomeFantasia.ToLower().Contains(b))
                             || (cnpj.Length > 0 && e.Cnpj.Contains(cnpj)));
        }

        return await q.OrderBy(e => e.RazaoSocial).ToListAsync(ct);
    }

    /// <summary>Carrega uma empresa sem navegações (própria para edição em formulário).</summary>
    public async Task<Empresa?> ObterParaEdicaoAsync(int id, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        return await ctx.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task SalvarAsync(Empresa e, CancellationToken ct = default)
    {
        e.Cnpj = CnpjValidator.Normalizar(e.Cnpj);
        e.RazaoSocial = (e.RazaoSocial ?? "").Trim();
        e.NomeFantasia = string.IsNullOrWhiteSpace(e.NomeFantasia) ? null : e.NomeFantasia.Trim();
        e.Observacoes = string.IsNullOrWhiteSpace(e.Observacoes) ? null : e.Observacoes.Trim();
        e.Analista = null;
        e.AnalistaBackup = null;

        if (!CnpjValidator.EhValido(e.Cnpj)) throw new ValidacaoException("CNPJ inválido. Confira os dígitos informados.");
        if (e.RazaoSocial.Length == 0) throw new ValidacaoException("Informe a razão social.");
        if (e.Regime is not (RegimeTributario.SimplesNacional or RegimeTributario.LucroPresumido or RegimeTributario.LucroReal))
            throw new ValidacaoException("Selecione o regime tributário.");
        if (e.AnalistaId != null && e.AnalistaId == e.AnalistaBackupId)
            throw new ValidacaoException("O analista de backup deve ser diferente do analista responsável.");

        await using var ctx = await _fabrica.CreateDbContextAsync(ct);

        if (await ctx.Empresas.AnyAsync(x => x.Cnpj == e.Cnpj && x.Id != e.Id, ct))
            throw new ValidacaoException("Já existe uma empresa cadastrada com este CNPJ.");

        if (e.Id == 0)
        {
            ctx.Empresas.Add(e);
        }
        else
        {
            var atual = await ctx.Empresas.FindAsync(new object[] { e.Id }, ct)
                        ?? throw new ValidacaoException("Empresa não encontrada.");
            if (atual.Versao != e.Versao) throw new ConcorrenciaException();

            var analistaAnterior = atual.AnalistaId;
            ctx.Entry(atual).CurrentValues.SetValues(e);

            // Ao trocar o analista, repassa as obrigações ainda abertas que estavam com o analista anterior.
            if (analistaAnterior != e.AnalistaId)
            {
                var abertas = await ctx.Obrigacoes
                    .Where(o => o.EmpresaId == e.Id && o.ResponsavelId == analistaAnterior
                                && (o.Status == StatusObrigacao.Pendente || o.Status == StatusObrigacao.EmAndamento))
                    .ToListAsync(ct);
                foreach (var o in abertas) o.ResponsavelId = e.AnalistaId;
            }
        }

        await Persistencia.SalvarAsync(ctx, ct);
    }

    public async Task DefinirAtivaAsync(int id, Guid versao, bool ativa, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var e = await ctx.Empresas.FindAsync(new object[] { id }, ct) ?? throw new ValidacaoException("Empresa não encontrada.");
        if (e.Versao != versao) throw new ConcorrenciaException();
        e.Ativa = ativa;
        await Persistencia.SalvarAsync(ctx, ct);
    }
}
