using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;
using ControleContabil.Infrastructure;

namespace ControleContabil.Desktop.ViewModels;

public partial class EmpresasViewModel : PaginaBase
{
    private readonly EmpresaService _empresas;
    private readonly UsuarioService _usuarios;
    private int _sequencia;

    public EmpresasViewModel(EmpresaService empresas, UsuarioService usuarios, IDialogoService dialogos) : base(dialogos)
    {
        _empresas = empresas;
        _usuarios = usuarios;
    }

    public override string Titulo => "Empresas clientes";

    public ObservableCollection<Empresa> Empresas { get; } = new();

    [ObservableProperty] private string _busca = "";
    [ObservableProperty] private bool _incluirInativas;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    [NotifyCanExecuteChangedFor(nameof(AlternarAtivaCommand))]
    private Empresa? _selecionada;

    partial void OnBuscaChanged(string value) => _ = CarregarAsync();
    partial void OnIncluirInativasChanged(bool value) => _ = CarregarAsync();

    public override async Task CarregarAsync()
    {
        await ExecutarAsync(async () =>
        {
            var token = ++_sequencia;
            var lista = await _empresas.ListarAsync(Busca, IncluirInativas);
            if (token != _sequencia) return; // uma busca mais nova já foi disparada

            var idSelecionado = Selecionada?.Id;
            Empresas.Clear();
            foreach (var e in lista) Empresas.Add(e);
            Selecionada = Empresas.FirstOrDefault(e => e.Id == idSelecionado);
            Mensagem = $"{Empresas.Count} empresa(s)";
        }, recarregarNoConflito: false);
    }

    [RelayCommand]
    private async Task NovaAsync()
    {
        var usuarios = await _usuarios.ListarAsync();
        var resultado = Dialogos.EditarEmpresa(new Empresa(), usuarios);
        if (resultado is null) return;

        await ExecutarAsync(async () =>
        {
            await _empresas.SalvarAsync(resultado);
            await CarregarAsync();
        });
    }

    private bool TemSelecao() => Selecionada is not null;

    [RelayCommand(CanExecute = nameof(TemSelecao))]
    private async Task EditarAsync()
    {
        if (Selecionada is null) return;
        var usuarios = await _usuarios.ListarAsync();
        var empresa = await _empresas.ObterParaEdicaoAsync(Selecionada.Id);
        if (empresa is null)
        {
            Dialogos.Aviso("A empresa não existe mais.");
            await CarregarAsync();
            return;
        }

        var resultado = Dialogos.EditarEmpresa(empresa, usuarios);
        if (resultado is null) return;

        await ExecutarAsync(async () =>
        {
            await _empresas.SalvarAsync(resultado);
            await CarregarAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(TemSelecao))]
    private async Task AlternarAtivaAsync()
    {
        if (Selecionada is not { } e) return;
        var acao = e.Ativa ? "inativar" : "reativar";
        if (!Dialogos.Confirmar($"Deseja {acao} a empresa \"{e.RazaoSocial}\"?\n" +
                                (e.Ativa ? "Empresas inativas deixam de receber novas obrigações e checklists." : ""))) return;

        await ExecutarAsync(async () =>
        {
            await _empresas.DefinirAtivaAsync(e.Id, e.Versao, !e.Ativa);
            await CarregarAsync();
        });
    }

    [RelayCommand]
    private void ExportarCsv()
    {
        var caminho = CaminhoCsv("empresas");
        if (string.IsNullOrEmpty(caminho)) return;
        try
        {
            CsvExport.Salvar(caminho, Empresas,
                ("CNPJ", e => CnpjValidator.Formatar(e.Cnpj)),
                ("Razão social", e => e.RazaoSocial),
                ("Nome fantasia", e => e.NomeFantasia),
                ("Regime", e => Rotulos.Regime(e.Regime)),
                ("Analista", e => e.Analista?.Nome),
                ("Analista backup", e => e.AnalistaBackup?.Nome),
                ("Ativa", e => e.Ativa));
            Dialogos.Info("Arquivo exportado com sucesso.");
        }
        catch (Exception ex)
        {
            Dialogos.Erro("Não foi possível gravar o arquivo.", ex);
        }
    }
}
