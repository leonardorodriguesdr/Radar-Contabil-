using ControleContabil.Core;
using Xunit;

namespace ControleContabil.Tests;

public class DiasUteisTests
{
    [Theory]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Pascoa_e_calculada_corretamente(int ano, int mes, int dia)
    {
        Assert.Equal(new DateOnly(ano, mes, dia), DiasUteis.Pascoa(ano));
    }

    [Fact]
    public void Carnaval_nao_e_dia_util()
    {
        // Páscoa 2026 = 5/abr. Terça de Carnaval = Páscoa − 47 dias = 17/fev.
        Assert.False(DiasUteis.EhUtil(new DateOnly(2026, 2, 17)));
        Assert.False(DiasUteis.EhUtil(new DateOnly(2026, 2, 16)));
    }

    [Fact]
    public void Finados_e_feriado_nacional()
    {
        // 2 de novembro de 2026 é uma segunda-feira.
        Assert.False(DiasUteis.EhUtil(new DateOnly(2026, 11, 2)));
    }

    [Fact]
    public void Primeiro_dia_util_de_janeiro_de_2026_pula_feriado_e_fim_de_semana()
    {
        // 1/jan/2026 é quinta-feira (feriado). O 1º dia útil é 2/jan (sexta).
        Assert.Equal(new DateOnly(2026, 1, 2), DiasUteis.NesimoDiaUtil(2026, 1, 1));
    }

    [Fact]
    public void Ultimo_dia_util_de_outubro_de_2026_e_30()
    {
        // 31/out/2026 é sábado.
        Assert.Equal(new DateOnly(2026, 10, 30), DiasUteis.UltimoDiaUtil(2026, 10));
    }

    [Fact]
    public void Ajustar_antecipa_para_o_dia_util_anterior()
    {
        Assert.Equal(new DateOnly(2026, 10, 30), DiasUteis.Ajustar(new DateOnly(2026, 10, 31), antecipar: true));
    }

    [Fact]
    public void Ajustar_prorroga_pulando_fim_de_semana_e_feriado()
    {
        // Sábado 31/out → domingo → segunda 2/nov (Finados) → terça 3/nov.
        Assert.Equal(new DateOnly(2026, 11, 3), DiasUteis.Ajustar(new DateOnly(2026, 10, 31), antecipar: false));
    }
}
