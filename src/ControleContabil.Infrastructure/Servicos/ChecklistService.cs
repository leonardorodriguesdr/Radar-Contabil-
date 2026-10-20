using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

public class ChecklistService
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;

    public ChecklistService(IDbContextFactory<AppDbContext> fabrica) => _fabrica = fabrica;

    // ----- Modelos -----

    public async Task<List<ChecklistModelo>> ListarModelosAsync(CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var modelos = await ctx.ChecklistModelos.AsNoTracking().Include(m => m.Itens).OrderBy(m => m.Nome).ToListAsync(ct);
        foreach (var m in modelos) m.Itens = m.Itens.OrderBy(i => i.Ordem).ToList();
        return modelos;
    }

    public async Task SalvarModeloAsync(ChecklistModelo m, IEnumerable<string> itens, CancellationToken ct = default)
    {
        m.Nome = (m.Nome ?? "").Trim();
        if (m.Nome.Length == 0) throw new ValidacaoException("Informe o nome do checklist.");
        if (m.EmpresaId == null && m.RegimesAplicaveis == RegimeTributario.Nenhum)
            throw new ValidacaoException("Selecione ao menos um regime ou uma empresa específica.");

        var descricoes = itens.Select(i => i.Trim()).Where(i => i.Length > 0).ToList();
        if (descricoes.Count == 0) throw new ValidacaoException("Informe ao menos um item para o checklist.");

        await using var ctx = await _fabrica.CreateDbContextAsync(ct);

        ChecklistModelo alvo;
        if (m.Id == 0)
        {
            alvo = new ChecklistModelo();
            ctx.ChecklistModelos.Add(alvo);
        }
        else
        {
            alvo = await ctx.ChecklistModelos.Include(x => x.Itens).FirstOrDefaultAsync(x => x.Id == m.Id, ct)
                   ?? throw new ValidacaoException("Checklist não encontrado.");
            if (alvo.Versao != m.Versao) throw new ConcorrenciaException();
        }

        alvo.Nome = m.Nome;
        alvo.RegimesAplicaveis = m.RegimesAplicaveis;
        alvo.EmpresaId = m.EmpresaId;
        alvo.Ativo = m.Ativo;

        // Reescreve a lista de itens (os checklists já gerados mantêm a cópia que receberam).
        ctx.ChecklistModeloItens.RemoveRange(alvo.Itens.ToList());
        alvo.Itens.Clear();
        for (var i = 0; i < descricoes.Count; i++)
            alvo.Itens.Add(new ChecklistModeloItem { Ordem = i + 1, Descricao = descricoes[i], Obrigatorio = true });

        await Persistencia.SalvarAsync(ctx, ct);
    }

    // ----- Execuções (checklists por empresa/competência) -----

    /// <summary>Cria, para todas as empresas ativas, os checklists aplicáveis da competência. Idempotente.</summary>
    public async Task<int> GerarCompetenciaAsync(int ano, int mes, CancellationToken ct = default)
    {
        var competencia = new DateOnly(ano, mes, 1);
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        ctx.AuditoriaDesativada = true;

        var empresas = await ctx.Empresas.Where(e => e.Ativa).ToListAsync(ct);
        var modelos = await ctx.ChecklistModelos.Include(m => m.Itens).Where(m => m.Ativo).ToListAsync(ct);
        var existentes = await ctx.ChecklistExecucoes
            .Where(x => x.Competencia == competencia && x.ModeloId != null)
            .Select(x => new { x.EmpresaId, ModeloId = x.ModeloId!.Value })
            .ToListAsync(ct);
        var jaExiste = existentes.Select(x => (x.EmpresaId, x.ModeloId)).ToHashSet();

        var criados = 0;
        foreach (var e in empresas)
        foreach (var m in modelos)
        {
            if (!ObrigacaoGerador.ChecklistAplica(m, e)) continue;
            if (jaExiste.Contains((e.Id, m.Id))) continue;

            ctx.ChecklistExecucoes.Add(new ChecklistExecucao
            {
                EmpresaId = e.Id,
                ModeloId = m.Id,
                Nome = m.Nome,
                Competencia = competencia,
                Itens = m.Itens.OrderBy(i => i.Ordem).Select(i => new ChecklistExecucaoItem
                {
                    Ordem = i.Ordem,
                    Descricao = i.Descricao,
                    Obrigatorio = i.Obrigatorio
                }).ToList()
            });
            criados++;
        }

        if (criados > 0)
            ctx.RegistrarManual("ChecklistExecucao", "Gerado", $"Geração de checklists da competência {competencia:MM/yyyy}: {criados} criado(s)");

        await Persistencia.SalvarAsync(ctx, ct);
        return criados;
    }

    public async Task<List<ChecklistResumo>> ListarExecucoesAsync(DateOnly competencia, int? empresaId, int? analistaId, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var q = ctx.ChecklistExecucoes.AsNoTracking().Include(x => x.Empresa).ThenInclude(e => e!.Analista)
            .Where(x => x.Competencia == competencia);
        if (empresaId is { } emp) q = q.Where(x => x.EmpresaId == emp);
        if (analistaId is { } an) q = q.Where(x => x.Empresa!.AnalistaId == an);

        return await q.OrderBy(x => x.Empresa!.RazaoSocial).ThenBy(x => x.Nome)
            .Select(x => new ChecklistResumo
            {
                Id = x.Id,
                EmpresaId = x.EmpresaId,
                EmpresaNome = x.Empresa!.RazaoSocial,
                AnalistaNome = x.Empresa.Analista != null ? x.Empresa.Analista.Nome : "—",
                Nome = x.Nome,
                Competencia = x.Competencia,
                Total = x.Itens.Count,
                Concluidos = x.Itens.Count(i => i.Concluido)
            })
            .ToListAsync(ct);
    }

    public async Task<List<ChecklistExecucaoItem>> ListarItensAsync(int execucaoId, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        return await ctx.ChecklistExecucaoItens.AsNoTracking()
            .Where(i => i.ExecucaoId == execucaoId).OrderBy(i => i.Ordem).ToListAsync(ct);
    }

    public async Task MarcarItemAsync(int itemId, Guid versao, bool concluido, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var item = await ctx.ChecklistExecucaoItens.FindAsync(new object[] { itemId }, ct)
                   ?? throw new ValidacaoException("Item não encontrado.");
        if (item.Versao != versao) throw new ConcorrenciaException();

        item.Concluido = concluido;
        if (concluido)
        {
            var nome = ctx.UsuarioAtualId is { } uid
                ? await ctx.Usuarios.Where(u => u.Id == uid).Select(u => u.Nome).FirstOrDefaultAsync(ct)
                : null;
            item.ConcluidoEm = DateTime.UtcNow;
            item.ConcluidoPorId = ctx.UsuarioAtualId;
            item.ConcluidoPorNome = nome;
        }
        else
        {
            item.ConcluidoEm = null;
            item.ConcluidoPorId = null;
            item.ConcluidoPorNome = null;
        }
        await Persistencia.SalvarAsync(ctx, ct);
    }
}
