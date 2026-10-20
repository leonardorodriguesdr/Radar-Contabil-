using CommunityToolkit.Mvvm.ComponentModel;
using ControleContabil.Core;
using ControleContabil.Infrastructure;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ControleContabil.Desktop.ViewModels;

public record ItemMenu(string Titulo, PackIconKind Icone, Type TipoViewModel);

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _servicos;
    private readonly UsuarioAtual _usuario;

    public MainViewModel(IServiceProvider servicos, UsuarioAtual usuario)
    {
        _servicos = servicos;
        _usuario = usuario;

        Menu = new ObservableCollection<ItemMenu>
        {
            new("Painel", PackIconKind.ViewDashboard, typeof(DashboardViewModel)),
            new("Empresas", PackIconKind.Domain, typeof(EmpresasViewModel)),
            new("Obrigações e prazos", PackIconKind.CalendarCheck, typeof(ObrigacoesViewModel)),
            new("Checklists", PackIconKind.ClipboardCheckOutline, typeof(ChecklistsViewModel)),
            new("Documentos", PackIconKind.FolderOpen, typeof(DocumentosViewModel)),
            new("Relatórios", PackIconKind.ChartBox, typeof(RelatoriosViewModel))
        };
        if (usuario.PodeAdministrar)
            Menu.Add(new ItemMenu("Configurações", PackIconKind.Cog, typeof(ConfiguracoesViewModel)));
    }

    public ObservableCollection<ItemMenu> Menu { get; }

    public string UsuarioNome => _usuario.Nome;
    public string PerfilTexto => Rotulos.Perfil(_usuario.Perfil);

    [ObservableProperty]
    private ItemMenu? _menuSelecionado;

    [ObservableProperty]
    private PaginaBase? _paginaAtual;

    public void Iniciar() => MenuSelecionado = Menu[0];

    partial void OnMenuSelecionadoChanged(ItemMenu? value)
    {
        if (value is not null) _ = NavegarAsync(value.TipoViewModel);
    }

    private async Task NavegarAsync(Type tipo)
    {
        var anterior = PaginaAtual;
        var pagina = (PaginaBase)_servicos.GetRequiredService(tipo);
        PaginaAtual = pagina;
        anterior?.Dispose();
        await pagina.CarregarAsync();
    }
}
