using ControleContabil.Desktop.Infra;
using ControleContabil.Desktop.ViewModels;
using ControleContabil.Desktop.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace ControleContabil.Desktop;

public static class DesktopServices
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        services.AddSingleton<IDialogoService, DialogoService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        // Páginas: uma instância nova a cada navegação (dados sempre frescos, timers descartados).
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<EmpresasViewModel>();
        services.AddTransient<ObrigacoesViewModel>();
        services.AddTransient<ChecklistsViewModel>();
        services.AddTransient<DocumentosViewModel>();
        services.AddTransient<RelatoriosViewModel>();
        services.AddTransient<ConfiguracoesViewModel>();
        return services;
    }
}
