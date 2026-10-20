using ControleContabil.Core;
using Xunit;

namespace ControleContabil.Tests;

public class PrazoServiceTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 20);

    [Theory]
    [InlineData(StatusObrigacao.Concluida)]
    [InlineData(StatusObrigacao.Dispensada)]
    public void Obrigacao_concluida_ou_dispensada_ignora_o_prazo(StatusObrigacao status)
    {
        // Vencida há 10 dias, mas já concluída: não é atrasada.
        var situacao = PrazoService.Situacao(status, Hoje.AddDays(-10), Hoje, 5);
        Assert.Equal(SituacaoPrazo.Concluida, situacao);
    }

    [Fact]
    public void Vencimento_anterior_a_hoje_e_atrasada()
    {
        var situacao = PrazoService.Situacao(StatusObrigacao.Pendente, Hoje.AddDays(-1), Hoje, 5);
        Assert.Equal(SituacaoPrazo.Atrasada, situacao);
    }

    [Fact]
    public void Vencimento_hoje_ainda_nao_e_atrasada()
    {
        var situacao = PrazoService.Situacao(StatusObrigacao.Pendente, Hoje, Hoje, 5);
        Assert.Equal(SituacaoPrazo.ProximoVencimento, situacao);
    }

    [Theory]
    [InlineData(5, SituacaoPrazo.ProximoVencimento)] // exatamente no limite do alerta
    [InlineData(6, SituacaoPrazo.EmDia)]             // um dia além do limite
    public void Limite_do_alerta_e_inclusivo(int diasAteVencer, SituacaoPrazo esperado)
    {
        var situacao = PrazoService.Situacao(StatusObrigacao.Pendente, Hoje.AddDays(diasAteVencer), Hoje, 5);
        Assert.Equal(esperado, situacao);
    }

    [Fact]
    public void Em_andamento_segue_as_mesmas_regras_de_prazo()
    {
        var situacao = PrazoService.Situacao(StatusObrigacao.EmAndamento, Hoje.AddDays(-2), Hoje, 5);
        Assert.Equal(SituacaoPrazo.Atrasada, situacao);
    }
}
