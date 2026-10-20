using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using ControleContabil.Desktop.Windows;
using ControleContabil.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControleContabil.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Servicos { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var cultura = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{args.Exception.Message}", "Controle Contábil",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = e.Args,
                ContentRootPath = AppContext.BaseDirectory
            });
            // Ajustes por máquina/ambiente sem mexer no appsettings.json distribuído.
            builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
            builder.Services.AddInfraestrutura(builder.Configuration);
            builder.Services.AddDesktop();

            _host = builder.Build();
            Servicos = _host.Services;

            await DbInicializador.InicializarAsync(Servicos.GetRequiredService<IDbContextFactory<AppDbContext>>());

            var usuarios = Servicos.GetRequiredService<UsuarioService>();
            var usuario = await usuarios.ObterPorLoginAsync(Environment.UserName);
            if (usuario is null)
            {
                // Login do Windows ainda não cadastrado: o usuário se identifica manualmente (rede interna de confiança).
                var login = new LoginWindow(await usuarios.ListarAsync());
                if (login.ShowDialog() != true || login.UsuarioEscolhido is null)
                {
                    Shutdown();
                    return;
                }
                usuario = login.UsuarioEscolhido;
            }

            var atual = Servicos.GetRequiredService<UsuarioAtual>();
            atual.Id = usuario.Id;
            atual.Nome = usuario.Nome;
            atual.Perfil = usuario.Perfil;

            var janela = Servicos.GetRequiredService<MainWindow>();
            MainWindow = janela;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            janela.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Não foi possível iniciar o sistema.\n\n" + ex.Message +
                "\n\nVerifique a conexão com o banco de dados e o arquivo appsettings.json.",
                "Controle Contábil", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
