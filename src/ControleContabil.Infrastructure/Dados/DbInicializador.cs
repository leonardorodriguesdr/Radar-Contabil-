using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

/// <summary>Cria o esquema (se necessário) e carrega os dados iniciais (modelos de obrigação e checklists).</summary>
public static class DbInicializador
{
    public static async Task InicializarAsync(IDbContextFactory<AppDbContext> fabrica, CancellationToken ct = default)
    {
        await using var ctx = await fabrica.CreateDbContextAsync(ct);
        ctx.AuditoriaDesativada = true;

        // v1: cria as tabelas na primeira execução. Para evoluir o esquema em produção,
        // adote EF Migrations (veja o README, seção "Evolução do banco").
        await ctx.Database.EnsureCreatedAsync(ct);

        if (!await ctx.Usuarios.AnyAsync(ct))
        {
            ctx.Usuarios.Add(new Usuario
            {
                Nome = Environment.UserName,
                LoginWindows = Environment.UserName,
                Perfil = PerfilUsuario.Administrador,
                Ativo = true
            });
        }

        if (!await ctx.ObrigacaoModelos.AnyAsync(ct))
            ctx.ObrigacaoModelos.AddRange(ModelosPadraoObrigacao());

        if (!await ctx.ChecklistModelos.AnyAsync(ct))
            ctx.ChecklistModelos.AddRange(ModelosPadraoChecklist());

        await ctx.SaveChangesAsync(ct);
    }

    private const RegimeTributario SN = RegimeTributario.SimplesNacional;
    private const RegimeTributario LP = RegimeTributario.LucroPresumido;
    private const RegimeTributario LR = RegimeTributario.LucroReal;
    private const RegimeTributario Todos = RegimeTributario.Todos;

    /// <summary>
    /// Catálogo inicial. Os prazos são PADRÕES SUGERIDOS (muitos são prazos internos do escritório) e devem ser
    /// conferidos com o calendário fiscal vigente e com as regras de cada UF/município. Tudo é editável em
    /// Configurações → Modelos de obrigação.
    /// </summary>
    public static List<ObrigacaoModelo> ModelosPadraoObrigacao() => new()
    {
        M("Conciliações contábeis", TipoObrigacao.Conciliacao, Periodicidade.Mensal, RegraVencimento.DiaFixo, 10, 1, 0, Todos, "Prazo interno de fechamento."),
        M("Balancete mensal", TipoObrigacao.Balancete, Periodicidade.Mensal, RegraVencimento.DiaFixo, 20, 1, 0, Todos, "Prazo interno."),
        M("DRE mensal", TipoObrigacao.DRE, Periodicidade.Mensal, RegraVencimento.DiaFixo, 20, 1, 0, Todos, "Prazo interno."),
        M("PGDAS-D / DAS", TipoObrigacao.ApuracaoImposto, Periodicidade.Mensal, RegraVencimento.DiaFixo, 20, 1, 0, SN, "Apuração e transmissão do Simples Nacional."),
        M("Apuração PIS/COFINS", TipoObrigacao.ApuracaoImposto, Periodicidade.Mensal, RegraVencimento.DiaFixo, 25, 1, 0, LP | LR, "DARF PIS/COFINS."),
        M("Apuração ICMS/ISS", TipoObrigacao.ApuracaoImposto, Periodicidade.Mensal, RegraVencimento.DiaFixo, 20, 1, 0, LP | LR, "Prazo varia por UF/município — ajuste conforme o caso."),
        M("DCTFWeb", TipoObrigacao.ObrigacaoAcessoria, Periodicidade.Mensal, RegraVencimento.DiaFixo, 15, 1, 0, LP | LR, null),
        M("EFD-Reinf", TipoObrigacao.ObrigacaoAcessoria, Periodicidade.Mensal, RegraVencimento.DiaFixo, 15, 1, 0, LP | LR, null),
        M("EFD-Contribuições", TipoObrigacao.ObrigacaoAcessoria, Periodicidade.Mensal, RegraVencimento.NesimoDiaUtil, 10, 2, 0, LP | LR, "10º dia útil do 2º mês subsequente."),
        M("Apuração IRPJ/CSLL — estimativa mensal", TipoObrigacao.ApuracaoImposto, Periodicidade.Mensal, RegraVencimento.UltimoDiaUtil, 0, 1, 0, LR, "Lucro Real com estimativa mensal; desative se a empresa apura por balancete de suspensão."),
        M("Apuração IRPJ/CSLL trimestral", TipoObrigacao.ApuracaoImposto, Periodicidade.Trimestral, RegraVencimento.UltimoDiaUtil, 0, 1, 0, LP, "Último dia útil do mês seguinte ao trimestre."),
        M("DEFIS", TipoObrigacao.ObrigacaoAcessoria, Periodicidade.Anual, RegraVencimento.DiaFixo, 31, 0, 3, SN, "Até 31/03 do ano seguinte."),
        M("Balanço patrimonial e DRE anuais", TipoObrigacao.Balancete, Periodicidade.Anual, RegraVencimento.DiaFixo, 30, 0, 4, Todos, "Prazo interno."),
        M("ECD", TipoObrigacao.ObrigacaoAcessoria, Periodicidade.Anual, RegraVencimento.UltimoDiaUtil, 0, 0, 5, LP | LR, "Último dia útil de maio. Lucro Presumido: verificar dispensa."),
        M("ECF", TipoObrigacao.ObrigacaoAcessoria, Periodicidade.Anual, RegraVencimento.UltimoDiaUtil, 0, 0, 7, LP | LR, "Último dia útil de julho.")
    };

    private static ObrigacaoModelo M(string nome, TipoObrigacao tipo, Periodicidade per, RegraVencimento regra,
        int dia, int mesesApos, int mesVenc, RegimeTributario regimes, string? descricao) => new()
    {
        Nome = nome,
        Tipo = tipo,
        Periodicidade = per,
        Regra = regra,
        Dia = dia,
        MesesAposCompetencia = mesesApos,
        MesVencimento = mesVenc == 0 ? 1 : mesVenc,
        RegimesAplicaveis = regimes,
        AntecipaDiaNaoUtil = true,
        Descricao = descricao,
        Ativo = true
    };

    public static List<ChecklistModelo> ModelosPadraoChecklist() => new()
    {
        C("Fechamento mensal — Simples Nacional", SN,
            "Importar e classificar extratos bancários",
            "Conferir notas fiscais de entrada e saída",
            "Conciliar contas bancárias",
            "Conciliar contas a pagar e a receber",
            "Conferir faturamento e Fator R",
            "Apurar PGDAS-D e emitir o DAS",
            "Emitir balancete",
            "Revisão do coordenador",
            "Arquivar documentos da competência"),
        C("Fechamento mensal — Lucro Presumido", LP,
            "Importar e classificar extratos bancários",
            "Conferir notas fiscais de entrada e saída",
            "Conciliar contas bancárias",
            "Conciliar contas a pagar e a receber",
            "Lançar folha de pagamento e provisões (férias e 13º)",
            "Apurar PIS/COFINS e impostos sobre a receita",
            "Apurar IRPJ/CSLL (trimestre)",
            "Emitir balancete e DRE",
            "Revisão do coordenador",
            "Arquivar documentos da competência"),
        C("Fechamento mensal — Lucro Real", LR,
            "Importar e classificar extratos bancários",
            "Conferir notas fiscais de entrada e saída",
            "Conciliar contas bancárias e demais contas patrimoniais",
            "Lançar folha de pagamento e provisões",
            "Calcular depreciação e amortização",
            "Apurar PIS/COFINS não cumulativos e créditos",
            "Atualizar LALUR/LACS (partes A e B)",
            "Apurar IRPJ/CSLL (estimativa ou balancete de suspensão)",
            "Emitir balancete e DRE",
            "Revisão do coordenador",
            "Arquivar documentos da competência")
    };

    private static ChecklistModelo C(string nome, RegimeTributario regimes, params string[] itens) => new()
    {
        Nome = nome,
        RegimesAplicaveis = regimes,
        Ativo = true,
        Itens = itens.Select((d, i) => new ChecklistModeloItem { Ordem = i + 1, Descricao = d, Obrigatorio = true }).ToList()
    };
}
