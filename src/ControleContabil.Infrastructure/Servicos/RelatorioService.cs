using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

public class RelatorioService
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly AlertaOptions _alerta;

    public RelatorioService(IDbContextFactory<AppDbContext> fabrica, AlertaOptions alerta)
    {
        _fabrica = fabrica;
        _alerta = alerta;
    }

    /// <summary>Produtividade por analista para obrigações com vencimento no período.</summary>
    public async Task<List<ProdutividadeLinha>> ProdutividadeAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var hoje = DataHora.Hoje();

        var usuarios = await ctx.Usuarios.AsNoTracking().OrderBy(u => u.Nome).ToListAsync(ct);
        var obrigacoes = await ctx.Obrigacoes.AsNoTracking()
            .Where(o => o.Vencimento >= de && o.Vencimento <= ate && o.ResponsavelId != null)
            .ToListAsync(ct);

        var inicioUtc = DataHora.InicioDoDiaUtc(de);
        var fimUtc = DataHora.InicioDoDiaUtc(ate.AddDays(1));
        var itens = await ctx.ChecklistExecucaoItens.AsNoTracking()
            .Where(i => i.Concluido && i.ConcluidoPorId != null && i.ConcluidoEm >= inicioUtc && i.ConcluidoEm < fimUtc)
            .Select(i => i.ConcluidoPorId)
            .ToListAsync(ct);

        var linhas = new List<ProdutividadeLinha>();
        foreach (var u in usuarios)
        {
            var minhas = obrigacoes.Where(o => o.ResponsavelId == u.Id).ToList();
            var concluidas = minhas.Where(o => o.Status == StatusObrigacao.Concluida).ToList();
            var noPrazo = concluidas.Count(o => o.ConcluidaEm is { } c && DataHora.ParaDataLocal(c) <= o.Vencimento);
            var checklist = itens.Count(x => x == u.Id);

            if (minhas.Count == 0 && checklist == 0) continue;

            linhas.Add(new ProdutividadeLinha
            {
                Analista = u.Nome,
                Total = minhas.Count(o => o.Status != StatusObrigacao.Dispensada),
                Concluidas = concluidas.Count,
                ConcluidasNoPrazo = noPrazo,
                ConcluidasComAtraso = concluidas.Count - noPrazo,
                EmAberto = minhas.Count(o => o.Status is StatusObrigacao.Pendente or StatusObrigacao.EmAndamento),
                Atrasadas = minhas.Count(o => PrazoService.Situacao(o.Status, o.Vencimento, hoje, _alerta.DiasAlerta) == SituacaoPrazo.Atrasada),
                ItensChecklistConcluidos = checklist
            });
        }
        return linhas.OrderByDescending(l => l.Concluidas).ThenBy(l => l.Analista).ToList();
    }

    /// <summary>Pendências em aberto agrupadas por cliente.</summary>
    public async Task<List<PendenciaClienteLinha>> PendenciasPorClienteAsync(CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var hoje = DataHora.Hoje();

        var abertas = await ctx.Obrigacoes.AsNoTracking()
            .Include(o => o.Empresa).ThenInclude(e => e!.Analista)
            .Where(o => (o.Status == StatusObrigacao.Pendente || o.Status == StatusObrigacao.EmAndamento) && o.Empresa!.Ativa)
            .ToListAsync(ct);

        return abertas
            .GroupBy(o => o.EmpresaId)
            .Select(g =>
            {
                var e = g.First().Empresa!;
                var situacoes = g.Select(o => (o, s: PrazoService.Situacao(o.Status, o.Vencimento, hoje, _alerta.DiasAlerta))).ToList();
                var futuras = g.Where(o => o.Vencimento >= hoje).Select(o => (DateOnly?)o.Vencimento).ToList();
                return new PendenciaClienteLinha
                {
                    EmpresaId = e.Id,
                    Empresa = e.RazaoSocial,
                    Cnpj = CnpjValidator.Formatar(e.Cnpj),
                    Regime = e.Regime,
                    Analista = e.Analista?.Nome ?? "—",
                    Abertas = g.Count(),
                    Atrasadas = situacoes.Count(x => x.s == SituacaoPrazo.Atrasada),
                    Proximas = situacoes.Count(x => x.s == SituacaoPrazo.ProximoVencimento),
                    ProximoVencimento = futuras.Count == 0 ? null : futuras.Min()
                };
            })
            .OrderByDescending(l => l.Atrasadas).ThenByDescending(l => l.Proximas).ThenBy(l => l.Empresa)
            .ToList();
    }

    public async Task<List<AuditoriaLinha>> AuditoriaAsync(AuditoriaFiltro f, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var q = ctx.AuditoriaLogs.AsNoTracking().AsQueryable();

        if (f.De is { } de) { var ini = DataHora.InicioDoDiaUtc(de); q = q.Where(l => l.DataHoraUtc >= ini); }
        if (f.Ate is { } ate) { var fim = DataHora.InicioDoDiaUtc(ate.AddDays(1)); q = q.Where(l => l.DataHoraUtc < fim); }
        if (f.UsuarioId is { } uid) q = q.Where(l => l.UsuarioId == uid);
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim().ToLower();
            q = q.Where(l => l.Descricao.ToLower().Contains(t) || l.Entidade.ToLower().Contains(t)
                             || (l.Detalhes != null && l.Detalhes.ToLower().Contains(t)));
        }

        var logs = await q.OrderByDescending(l => l.DataHoraUtc).Take(Math.Clamp(f.Limite, 1, 5000)).ToListAsync(ct);

        var empresaIds = logs.Where(l => l.EmpresaId != null).Select(l => l.EmpresaId!.Value).Distinct().ToList();
        var nomes = await ctx.Empresas.AsNoTracking().Where(e => empresaIds.Contains(e.Id))
            .Select(e => new { e.Id, e.RazaoSocial }).ToDictionaryAsync(e => e.Id, e => e.RazaoSocial, ct);

        return logs.Select(l => new AuditoriaLinha
        {
            DataHoraLocal = DataHora.ParaLocal(l.DataHoraUtc),
            Usuario = l.UsuarioNome,
            Acao = l.Acao,
            Entidade = NomeEntidade(l.Entidade),
            Descricao = l.Descricao,
            Detalhes = l.Detalhes,
            Empresa = l.EmpresaId is { } id && nomes.TryGetValue(id, out var n) ? n : ""
        }).ToList();
    }

    private static string NomeEntidade(string e) => e switch
    {
        nameof(Empresa) => "Empresa",
        nameof(Usuario) => "Usuário",
        nameof(Obrigacao) => "Obrigação",
        nameof(ObrigacaoModelo) => "Modelo de obrigação",
        nameof(ChecklistModelo) => "Modelo de checklist",
        nameof(ChecklistExecucao) => "Checklist",
        nameof(ChecklistExecucaoItem) => "Item de checklist",
        nameof(Documento) => "Documento",
        _ => e
    };
}
