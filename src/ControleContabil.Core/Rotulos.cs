namespace ControleContabil.Core;

/// <summary>Textos em português para exibição dos enums.</summary>
public static class Rotulos
{
    public static string Regime(RegimeTributario r)
    {
        if (r == RegimeTributario.Nenhum) return "—";
        if (r == RegimeTributario.Todos) return "Todos";
        var partes = new List<string>();
        if (r.HasFlag(RegimeTributario.SimplesNacional)) partes.Add("Simples Nacional");
        if (r.HasFlag(RegimeTributario.LucroPresumido)) partes.Add("Lucro Presumido");
        if (r.HasFlag(RegimeTributario.LucroReal)) partes.Add("Lucro Real");
        return string.Join(" / ", partes);
    }

    public static string Perfil(PerfilUsuario p) => p switch
    {
        PerfilUsuario.Analista => "Analista",
        PerfilUsuario.Coordenador => "Coordenador",
        PerfilUsuario.Administrador => "Administrador",
        _ => p.ToString()
    };

    public static string Tipo(TipoObrigacao t) => t switch
    {
        TipoObrigacao.Conciliacao => "Conciliação",
        TipoObrigacao.Balancete => "Balancete",
        TipoObrigacao.DRE => "DRE",
        TipoObrigacao.ApuracaoImposto => "Apuração de imposto",
        TipoObrigacao.ObrigacaoAcessoria => "Obrigação acessória",
        TipoObrigacao.Fechamento => "Fechamento",
        TipoObrigacao.Outro => "Outro",
        _ => t.ToString()
    };

    public static string Periodicidade(Periodicidade p) => p switch
    {
        Core.Periodicidade.Mensal => "Mensal",
        Core.Periodicidade.Trimestral => "Trimestral",
        Core.Periodicidade.Anual => "Anual",
        _ => p.ToString()
    };

    public static string Regra(RegraVencimento r) => r switch
    {
        RegraVencimento.DiaFixo => "Dia fixo",
        RegraVencimento.NesimoDiaUtil => "N-ésimo dia útil",
        RegraVencimento.UltimoDiaUtil => "Último dia útil",
        _ => r.ToString()
    };

    public static string Status(StatusObrigacao s) => s switch
    {
        StatusObrigacao.Pendente => "Pendente",
        StatusObrigacao.EmAndamento => "Em andamento",
        StatusObrigacao.Concluida => "Concluída",
        StatusObrigacao.Dispensada => "Dispensada",
        _ => s.ToString()
    };

    public static string Situacao(SituacaoPrazo s) => s switch
    {
        SituacaoPrazo.EmDia => "Em dia",
        SituacaoPrazo.ProximoVencimento => "Próximo do vencimento",
        SituacaoPrazo.Atrasada => "Atrasada",
        SituacaoPrazo.Concluida => "Finalizada",
        _ => s.ToString()
    };

    /// <summary>Despacha para o rótulo correto conforme o tipo do enum (usado por conversores de XAML).</summary>
    public static string Para(object? valor) => valor switch
    {
        null => "",
        RegimeTributario r => Regime(r),
        PerfilUsuario p => Perfil(p),
        TipoObrigacao t => Tipo(t),
        Periodicidade p => Periodicidade(p),
        RegraVencimento r => Regra(r),
        StatusObrigacao s => Status(s),
        SituacaoPrazo s => Situacao(s),
        _ => valor.ToString() ?? ""
    };
}
