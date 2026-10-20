using ControleContabil.Core;
using Xunit;

namespace ControleContabil.Tests;

public class ObrigacaoGeradorTests
{
    private static Empresa EmpresaSimples(bool ativa = true) => new()
    {
        Id = 1,
        Cnpj = "11222333000181",
        RazaoSocial = "Empresa Teste Ltda",
        Regime = RegimeTributario.SimplesNacional,
        Ativa = ativa
    };

    private static ObrigacaoModelo Modelo(Periodicidade periodicidade = Periodicidade.Mensal) => new()
    {
        Id = 10,
        Nome = "Lançamento da folha",
        Periodicidade = periodicidade,
        Regra = RegraVencimento.DiaFixo,
        Dia = 20,
        MesesAposCompetencia = 1,
        AntecipaDiaNaoUtil = true,
        RegimesAplicaveis = RegimeTributario.Todos
    };

    [Fact]
    public void Modelo_mensal_gera_todo_mes_para_empresa_do_regime()
    {
        Assert.True(ObrigacaoGerador.DeveGerar(Modelo(), EmpresaSimples(), new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void Nao_gera_para_regime_fora_do_modelo()
    {
        var modelo = Modelo();
        modelo.RegimesAplicaveis = RegimeTributario.LucroReal;

        Assert.False(ObrigacaoGerador.DeveGerar(modelo, EmpresaSimples(), new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void Nao_gera_para_empresa_inativa()
    {
        Assert.False(ObrigacaoGerador.DeveGerar(Modelo(), EmpresaSimples(ativa: false), new DateOnly(2026, 9, 1)));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(6, true)]
    public void Modelo_trimestral_gera_somente_nos_meses_de_fechamento(int mes, bool esperado)
    {
        var resultado = ObrigacaoGerador.DeveGerar(
            Modelo(Periodicidade.Trimestral), EmpresaSimples(), new DateOnly(2026, mes, 1));
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Vencimento_mensal_cai_no_mes_seguinte_no_dia_configurado()
    {
        // Competência setembro/2026 → vence 20/out/2026 (terça-feira, dia útil).
        var vencimento = ObrigacaoGerador.CalcularVencimento(Modelo(), new DateOnly(2026, 9, 1));
        Assert.Equal(new DateOnly(2026, 10, 20), vencimento);
    }

    [Fact]
    public void Vencimento_em_fim_de_semana_e_antecipado_para_sexta()
    {
        // Competência dezembro/2026 → vence 20/mar/2027, sábado. Antecipa para sexta, 19/mar.
        var modelo = Modelo(Periodicidade.Anual);
        modelo.MesVencimento = 3;

        var vencimento = ObrigacaoGerador.CalcularVencimento(modelo, new DateOnly(2026, 12, 1));
        Assert.Equal(new DateOnly(2027, 3, 19), vencimento);
    }

    [Fact]
    public void Criar_copia_dados_do_modelo_e_do_analista()
    {
        var empresa = EmpresaSimples();
        empresa.AnalistaId = 7;

        var obrigacao = ObrigacaoGerador.Criar(Modelo(), empresa, new DateOnly(2026, 9, 15));

        Assert.Equal(StatusObrigacao.Pendente, obrigacao.Status);
        Assert.Equal(new DateOnly(2026, 9, 1), obrigacao.Competencia);
        Assert.Equal(new DateOnly(2026, 10, 20), obrigacao.Vencimento);
        Assert.Equal(7, obrigacao.ResponsavelId);
        Assert.Equal("Lançamento da folha", obrigacao.Nome);
    }
}
