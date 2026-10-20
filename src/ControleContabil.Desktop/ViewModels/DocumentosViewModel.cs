using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;
using ControleContabil.Infrastructure;

namespace ControleContabil.Desktop.ViewModels;

public partial class DocumentosViewModel : PaginaBase
{
    private readonly DocumentoService _documentos;
    private readonly EmpresaService _empresas;
    private readonly ObrigacaoService _obrigacoes;
    private bool _inicializando = true;
    private int _sequencia;

    public DocumentosViewModel(DocumentoService documentos, EmpresaService empresas, ObrigacaoService obrigacoes,
        IDialogoService dialogos) : base(dialogos)
    {
        _documentos = documentos;
        _empresas = empresas;
        _obrigacoes = obrigacoes;
    }

    public override string Titulo => "Documentos";

    public ObservableCollection<DocumentoLinha> Documentos { get; } = new();

    [ObservableProperty] private List<Empresa> _empresasFiltro = new();
    [ObservableProperty] private Empresa? _empresaSelecionada;
    [ObservableProperty] private string _texto = "";
    [ObservableProperty] private List<ObrigacaoItem> _obrigacoesDaEmpresa = new();
    [ObservableProperty] private ObrigacaoItem? _obrigacaoSelecionada;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirCommand))]
    [NotifyCanExecuteChangedFor(nameof(MostrarNaPastaCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoverCommand))]
    private DocumentoLinha? _selecionado;

    public bool EmpresaEscolhida => EmpresaSelecionada is { Id: > 0 };

    partial void OnTextoChanged(string value) => Recarregar();

    partial void OnEmpresaSelecionadaChanged(Empresa? value)
    {
        OnPropertyChanged(nameof(EmpresaEscolhida));
        EnviarCommand.NotifyCanExecuteChanged();
        _ = CarregarObrigacoesDaEmpresaAsync();
        Recarregar();
    }

    private void Recarregar()
    {
        if (!_inicializando) _ = CarregarAsync();
    }

    private async Task CarregarObrigacoesDaEmpresaAsync()
    {
        if (EmpresaSelecionada is not { Id: > 0 } e)
        {
            ObrigacoesDaEmpresa = new List<ObrigacaoItem>();
            return;
        }

        try
        {
            var lista = await _obrigacoes.ListarAsync(new ObrigacaoFiltro { EmpresaId = e.Id });
            // Vínculo opcional: o mais recente primeiro; limita para a lista continuar útil.
            ObrigacoesDaEmpresa = lista.OrderByDescending(i => i.Vencimento).Take(60).ToList();
            ObrigacaoSelecionada = null;
        }
        catch (Exception ex)
        {
            Dialogos.Erro("Não foi possível carregar as obrigações da empresa.", ex);
        }
    }

    public override async Task CarregarAsync()
    {
        await ExecutarAsync(async () =>
        {
            if (_inicializando)
            {
                var empresas = await _empresas.ListarAsync(incluirInativas: true);
                EmpresasFiltro = new[] { new Empresa { Id = 0, RazaoSocial = "Todas as empresas" } }.Concat(empresas).ToList();
                EmpresaSelecionada = EmpresasFiltro[0];
                _inicializando = false;
            }

            var token = ++_sequencia;
            var lista = await _documentos.ListarAsync(
                EmpresaSelecionada is { Id: > 0 } e ? e.Id : null, Texto);
            if (token != _sequencia) return;

            Documentos.Clear();
            foreach (var d in lista) Documentos.Add(d);
            Mensagem = $"{Documentos.Count} documento(s)";
        }, recarregarNoConflito: false);
    }

    private bool PodeEnviar() => EmpresaEscolhida;

    [RelayCommand(CanExecute = nameof(PodeEnviar))]
    private async Task EnviarAsync()
    {
        if (EmpresaSelecionada is not { Id: > 0 } empresa) return;
        var arquivos = Dialogos.EscolherArquivos();
        if (arquivos.Count == 0) return;

        await ExecutarAsync(async () =>
        {
            var qtd = await _documentos.EnviarAsync(empresa.Id, ObrigacaoSelecionada?.Id, arquivos);
            Dialogos.Info($"{qtd} documento(s) enviado(s) para o repositório.");
            await CarregarAsync();
        });
    }

    private bool TemSelecao() => Selecionado is not null;

    [RelayCommand(CanExecute = nameof(TemSelecao))]
    private void Abrir()
    {
        if (Selecionado is { } d) Dialogos.Abrir(_documentos.CaminhoCompleto(d.CaminhoRelativo));
    }

    [RelayCommand(CanExecute = nameof(TemSelecao))]
    private void MostrarNaPasta()
    {
        if (Selecionado is { } d) Dialogos.MostrarNaPasta(_documentos.CaminhoCompleto(d.CaminhoRelativo));
    }

    [RelayCommand(CanExecute = nameof(TemSelecao))]
    private async Task RemoverAsync()
    {
        if (Selecionado is not { } d) return;
        if (!Dialogos.Confirmar($"Remover o documento \"{d.NomeArquivo}\"?\nO arquivo será apagado do repositório. Esta ação não pode ser desfeita.")) return;

        await ExecutarAsync(async () =>
        {
            await _documentos.RemoverAsync(d.Id, d.Versao);
            await CarregarAsync();
        });
    }
}
