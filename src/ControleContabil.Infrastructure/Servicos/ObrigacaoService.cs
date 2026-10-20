using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

public class ObrigacaoService
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly AlertaOptions _alerta;

    public ObrigacaoService(IDbContextFactory<AppDbContext> fabrica, AlertaOptions alerta)
    {
        _fabrica = fabrica;
        _alerta = alerta;
    }

    public async Task<List<ObrigacaoItem>> ListarAsync(ObrigacaoFiltro f, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var q = ctx.Obrigacoes.AsNoTracking()
            .Include(o => o.Empresa)
            .Include(o => o.Responsavel)
            .AsQueryable();

        if (f.VencimentoDe is { } de && f.VencimentoAte is { } ate)
        {
            var incluir = f.IncluirAtrasadasAnteriores;
            q = q.Where(o => (o.Vencimento >= de && o.Vencimento <= ate)
                             || (incluir && o.Vencimento < de
                                 && (o.Status == StatusObrigacao.Pendente || o.Status == StatusObrigacao.EmAndamento)));
        }
        else
        {
            if (f.VencimentoDe is { } soDe) q = q.Where(o => o.Vencimento >= soDe);
            if (f.VencimentoAte is { } soAte) q = q.Where(o => o.Vencimento <= soAte);
        }

        if (f.EmpresaId is { } emp) q = q.Where(o => o.EmpresaId == emp);
        if (f.ResponsavelId is { } resp) q = q.Where(o => o.ResponsavelId == resp);

        var lista = await q.OrderBy(o => o.Vencimento).ThenBy(o => o.Empresa!.RazaoSocial).ToListAsync(ct);

        var hoje = DataHora.Hoje();
        var itens = lista.Select(o => ParaItem(o, hoje)).AsEnumerable();

        if (f.Situacao is { } sit) itens = itens.Where(i => i.Situacao == sit);
        if (f.OcultarFinalizadas) itens = itens.Where(i => i.Aberta);
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            itens = itens.Where(i => i.Nome.Contains(t, StringComparison.CurrentCultureIgnoreCase)
                                     || i.EmpresaNome.Contains(t, StringComparison.CurrentCultureIgnoreCase));
        }

        return itens.ToList();
    }

    private ObrigacaoItem ParaItem(Obrigacao o, DateOnly hoje) => new()
    {
        Id = o.Id,
        Versao = o.Versao,
        EmpresaId = o.EmpresaId,
        EmpresaNome = o.Empresa?.RazaoSocial ?? "",
        EmpresaCnpj = CnpjValidator.Formatar(o.Empresa?.Cnpj),
        Regime = o.Empresa?.Regime ?? RegimeTributario.Nenhum,
        ResponsavelId = o.ResponsavelId,
        ResponsavelNome = o.Responsavel?.Nome ?? "—",
        Nome = o.Nome,
        Tipo = o.Tipo,
        Periodicidade = o.Periodicidade,
        Competencia = o.Competencia,
        Vencimento = o.Vencimento,
        Status = o.Status,
        Situacao = PrazoService.Situacao(o.Status, o.Vencimento, hoje, _alerta.DiasAlerta),
        DiasParaVencer = o.Vencimento.DayNumber - hoje.DayNumber,
        ConcluidaEm = o.ConcluidaEm,
        Observacoes = o.Observacoes
    };

    /// <summary>Gera as obrigações da competência (mês) para todas as empresas ativas. Idempotente.</summary>
    /// <returns>Quantidade de obrigações criadas.</returns>
    public async Task<int> GerarCompetenciaAsync(int ano, int mes, CancellationToken ct = default)
    {
        var competencia = new DateOnly(ano, mes, 1);
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        ctx.AuditoriaDesativada = true;

        var empresas = await ctx.Empresas.Where(e => e.Ativa).ToListAsync(ct);
        var modelos = await ctx.ObrigacaoModelos.Where(m => m.Ativo).ToListAsync(ct);

        var existentes = await ctx.Obrigacoes
            .Where(o => o.Competencia == competencia && o.ModeloId != null)
            .Select(o => new { o.EmpresaId, ModeloId = o.ModeloId!.Value })
            .ToListAsync(ct);
        var jaExiste = existentes.Select(x => (x.EmpresaId, x.ModeloId)).ToHashSet();

        var criadas = 0;
        foreach (var e in empresas)
        foreach (var m in modelos)
        {
            if (!ObrigacaoGerador.DeveGerar(m, e, competencia)) continue;
            if (jaExiste.Contains((e.Id, m.Id))) continue;
            ctx.Obrigacoes.Add(ObrigacaoGerador.Criar(m, e, competencia));
            criadas++;
        }

        if (criadas > 0)
            ctx.RegistrarManual("Obrigacao", "Gerado", $"Geração de obrigações da competência {competencia:MM/yyyy}: {criadas} criada(s)");

        await Persistencia.SalvarAsync(ctx, ct);
        return criadas;
    }

    public Task IniciarAsync(IEnumerable<ObrigacaoItem> itens, CancellationToken ct = default) =>
        AlterarStatusAsync(itens, StatusObrigacao.EmAndamento, ct);

    public Task ConcluirAsync(IEnumerable<ObrigacaoItem> itens, CancellationToken ct = default) =>
        AlterarStatusAsync(itens, StatusObrigacao.Concluida, ct);

    public Task DispensarAsync(IEnumerable<ObrigacaoItem> itens, CancellationToken ct = default) =>
        AlterarStatusAsync(itens, StatusObrigacao.Dispensada, ct);

    public Task ReabrirAsync(IEnumerable<ObrigacaoItem> itens, CancellationToken ct = default) =>
        AlterarStatusAsync(itens, StatusObrigacao.Pendente, ct);

    private async Task AlterarStatusAsync(IEnumerable<ObrigacaoItem> itens, StatusObrigacao novo, CancellationToken ct)
    {
        var lista = itens.ToList();
        if (lista.Count == 0) return;

        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var ids = lista.Select(i => i.Id).ToList();
        var obrigacoes = await ctx.Obrigacoes.Where(o => ids.Contains(o.Id)).ToListAsync(ct);

        foreach (var item in lista)
        {
            var o = obrigacoes.FirstOrDefault(x => x.Id == item.Id)
                    ?? throw new ValidacaoException("Uma das obrigações selecionadas não existe mais.");
            if (o.Versao != item.Versao) throw new ConcorrenciaException();
            if (o.Status == novo) continue;

            o.Status = novo;
            if (novo == StatusObrigacao.Concluida)
            {
                o.ConcluidaEm = DateTime.UtcNow;
                o.ConcluidaPorId = ctx.UsuarioAtualId;
            }
            else
            {
                o.ConcluidaEm = null;
                o.ConcluidaPorId = null;
            }
        }

        await Persistencia.SalvarAsync(ctx, ct);
    }

    public async Task<ObrigacaoEdicao?> ObterParaEdicaoAsync(int id, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var o = await ctx.Obrigacoes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? null : new ObrigacaoEdicao
        {
            Id = o.Id, Versao = o.Versao, EmpresaId = o.EmpresaId, Nome = o.Nome, Tipo = o.Tipo,
            Competencia = o.Competencia, Vencimento = o.Vencimento, ResponsavelId = o.ResponsavelId,
            Observacoes = o.Observacoes
        };
    }

    /// <summary>Cria uma obrigação avulsa (Id = 0) ou altera nome, vencimento, responsável e observações.</summary>
    public async Task SalvarAsync(ObrigacaoEdicao d, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(d.Nome)) throw new ValidacaoException("Informe o nome da obrigação.");
        if (d.EmpresaId == 0) throw new ValidacaoException("Selecione a empresa.");

        await using var ctx = await _fabrica.CreateDbContextAsync(ct);

        if (d.Id == 0)
        {
            ctx.Obrigacoes.Add(new Obrigacao
            {
                EmpresaId = d.EmpresaId,
                Nome = d.Nome.Trim(),
                Tipo = d.Tipo,
                Periodicidade = Periodicidade.Mensal,
                Competencia = new DateOnly(d.Competencia.Year, d.Competencia.Month, 1),
                Vencimento = d.Vencimento,
                ResponsavelId = d.ResponsavelId,
                Observacoes = string.IsNullOrWhiteSpace(d.Observacoes) ? null : d.Observacoes.Trim()
            });
        }
        else
        {
            var o = await ctx.Obrigacoes.FindAsync(new object[] { d.Id }, ct)
                    ?? throw new ValidacaoException("Obrigação não encontrada.");
            if (o.Versao != d.Versao) throw new ConcorrenciaException();
            o.Nome = d.Nome.Trim();
            o.Tipo = d.Tipo;
            o.Vencimento = d.Vencimento;
            o.ResponsavelId = d.ResponsavelId;
            o.Observacoes = string.IsNullOrWhiteSpace(d.Observacoes) ? null : d.Observacoes.Trim();
        }

        await Persistencia.SalvarAsync(ctx, ct);
    }

    // ----- Catálogo de modelos -----

    public async Task<List<ObrigacaoModelo>> ListarModelosAsync(CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        return await ctx.ObrigacaoModelos.AsNoTracking().OrderBy(m => m.Periodicidade).ThenBy(m => m.Nome).ToListAsync(ct);
    }

    public async Task SalvarModeloAsync(ObrigacaoModelo m, CancellationToken ct = default)
    {
        m.Nome = (m.Nome ?? "").Trim();
        if (m.Nome.Length == 0) throw new ValidacaoException("Informe o nome do modelo.");
        if (m.RegimesAplicaveis == RegimeTributario.Nenhum) throw new ValidacaoException("Selecione ao menos um regime.");
        if (m.Regra != RegraVencimento.UltimoDiaUtil && (m.Dia < 1 || m.Dia > 31))
            throw new ValidacaoException("Informe um dia entre 1 e 31.");
        if (m.Periodicidade == Periodicidade.Anual && (m.MesVencimento < 1 || m.MesVencimento > 12))
            throw new ValidacaoException("Informe o mês de vencimento (1 a 12).");
        if (m.MesesAposCompetencia < 0 || m.MesesAposCompetencia > 12)
            throw new ValidacaoException("Os meses após a competência devem estar entre 0 e 12.");

        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        if (m.Id == 0)
        {
            ctx.ObrigacaoModelos.Add(m);
        }
        else
        {
            var atual = await ctx.ObrigacaoModelos.FindAsync(new object[] { m.Id }, ct)
                        ?? throw new ValidacaoException("Modelo não encontrado.");
            if (atual.Versao != m.Versao) throw new ConcorrenciaException();
            ctx.Entry(atual).CurrentValues.SetValues(m);
        }
        await Persistencia.SalvarAsync(ctx, ct);
    }
}
