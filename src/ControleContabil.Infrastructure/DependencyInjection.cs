using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControleContabil.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra banco de dados, armazenamento de documentos e serviços de aplicação.
    /// Lê as seções "Banco", "Armazenamento" e "Alertas" do appsettings.json.</summary>
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(config.GetSection("Banco").Get<BancoOptions>() ?? new BancoOptions());
        services.AddSingleton(config.GetSection("Armazenamento").Get<ArmazenamentoOptions>() ?? new ArmazenamentoOptions());
        services.AddSingleton(config.GetSection("Alertas").Get<AlertaOptions>() ?? new AlertaOptions());

        services.AddSingleton<UsuarioAtual>();
        services.AddSingleton<IDbContextFactory<AppDbContext>, AppDbContextFactory>();
        services.AddSingleton<IArmazenamentoDocumentos, ArmazenamentoLocal>();

        services.AddSingleton<UsuarioService>();
        services.AddSingleton<EmpresaService>();
        services.AddSingleton<ObrigacaoService>();
        services.AddSingleton<ChecklistService>();
        services.AddSingleton<DocumentoService>();
        services.AddSingleton<RelatorioService>();
        return services;
    }
}
