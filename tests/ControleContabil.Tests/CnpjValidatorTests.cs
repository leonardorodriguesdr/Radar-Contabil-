using ControleContabil.Core;
using Xunit;

namespace ControleContabil.Tests;

public class CnpjValidatorTests
{
    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    public void CNPJ_valido_e_aceito_com_ou_sem_mascara(string cnpj)
    {
        Assert.True(CnpjValidator.EhValido(cnpj));
    }

    [Theory]
    [InlineData("11.222.333/0001-82")] // dígito verificador errado
    [InlineData("11111111111111")]     // todos os dígitos iguais
    [InlineData("1122233300018")]      // 13 caracteres
    [InlineData("")]
    [InlineData(null)]
    public void CNPJ_invalido_e_rejeitado(string? cnpj)
    {
        Assert.False(CnpjValidator.EhValido(cnpj));
    }

    [Fact]
    public void Normalizar_remove_mascara_e_coloca_em_maiusculas()
    {
        Assert.Equal("11222333000181", CnpjValidator.Normalizar("11.222.333/0001-81"));
    }

    [Fact]
    public void Formatar_aplica_mascara_padrao()
    {
        Assert.Equal("11.222.333/0001-81", CnpjValidator.Formatar("11222333000181"));
    }

    [Fact]
    public void Formatar_devolve_o_original_quando_nao_tem_14_caracteres()
    {
        Assert.Equal("123", CnpjValidator.Formatar("123"));
    }
}
