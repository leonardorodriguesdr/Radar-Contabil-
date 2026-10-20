using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;
using ControleContabil.Infrastructure;

namespace ControleContabil.Desktop.ViewModels;

/// <summary>Item de checklist exibido com caixa de seleção. A gravação é imediata e protegida por versão.</summary>
public partial class ChecklistItemViewModel : ObservableObject
{
    private readonly ChecklistService _servico;
    private readonly Func<Task> _aoFalhar;
    private readonly Action<ChecklistItemViewModel> _aoAlterar;
    private Guid _versao;
    private bool _revertendo;

    public ChecklistItemViewModel(ChecklistExecucaoItem item, ChecklistService servico,
        Action<ChecklistItemViewModel> aoAlterar, Func<Task> aoFalhar)
    {
        Id = item.Id;
        Descricao = item.Descricao;
        Obrigatorio = item.Obrigatorio;
        _versao = item.Versao;
        _concluido = item.Concluido;
        _detalhe = item.Concluido && item.ConcluidoEm is { } em
            ? $"{item.ConcluidoPorNome ?? "—"} em {DataHora.ParaLocal(em):dd/MM HH:mm}"
            : "";
        _servico = servico;
        _aoAlterar = aoAlterar;
        _aoFalhar = aoFalhar;
    }

    public int Id { get; }
    public string Descricao { get; }
    public bool Obrigatorio { get; }

    [ObservableProperty] private bool _concluido;
    [ObservableProperty] private string _detalhe;

    partial void OnConcluidoChanged(bool value)
    {
        if (_revertendo) return;
        _ = GravarAsync(value);
    }

    private async Task GravarAsync(bool valor)
    {
        try
        {
            await _servico.MarcarItemAsync(Id, _versao, valor);
            _aoAlterar(this);
        }
        catch
        {
            _revertendo = true;
            Concluido = !valor;
            _revertendo = false;
            await _aoFalhar();
        }
    }
}

public partial class ChecklistsViewModel : PaginaBase
{
    private readonly ChecklistService _checklists;
    private readonly EmpresaService _empresas;
    private readonly UsuarioService _usuarios;
    private bool _inicializando = true;

    public ChecklistsViewModel(ChecklistService checklists, EmpresaService empresas, UsuarioService usuarios,
        IDialogoService dialogos) : base(dialogos)
    {
        _checklists = checklists;
        _empresas = empresas;
        _usuarios = usuarios;

        var hoje = DataHora.Hoje();
        _mesSelecionado = Meses.First(m => m.Valor.Year == hoje.Year && m.Valor.Month == hoje.Month);
    }

    public override string Titulo => "Checklists de fechamento";

    public List<MesItem> Meses { get; } = Opcoes.Meses(12, 3);

    public ObservableCollection<ChecklistResumo> Execucoes { get; } = new();
    public ObservableCollection<ChecklistItemViewModel> Itens { get; } = new();

    [ObservableProperty] private List<Empresa> _empresasFiltro = new();
    [ObservableProperty] private List<Usuario> _analistasFiltro = new();
    [ObservableProperty] private MesItem? _mesSelecionado;
    [ObservableProperty] private Empresa? _empresaSelecionada;
    [ObservableProperty] private Usuario? _analistaSelecionado;

    [ObservableProperty] private ChecklistResumo? _execucaoSelecionada;
    [ObservableProperty] private string _tituloDetalhe = "Selecione um checklist à esquerda";

    partial void OnMesSelecionadoChanged(MesItem? value) => Recarregar();
    partial void OnEmpresaSelecionadaChanged(Empresa? value) => Recarregar();
    partial void OnAnalistaSelecionadoChanged(Usuario? value) => Recarregar();

    partial void OnExecucaoSelecionadaChanged(ChecklistResumo? value) => _ = CarregarItensAsync();

    private void Recarregar()
    {
        if (!_inicializando) _ = CarregarAsync();
    }

    public override async Task CarregarAsync()
    {
        await ExecutarAsync(async () =>
        {
            if (_inicializando)
            {
                var empresas = await _empresas.ListarAsync();
                var usuarios = await _usuarios.ListarAsync();
                EmpresasFiltro = new[] { new Empresa { Id = 0, RazaoSocial = "Todas as empresas" } }.Concat(empresas).ToList();
                AnalistasFiltro = new[] { new Usuario { Id = 0, Nome = "Todos os analistas" } }.Concat(usuarios).ToList();
                EmpresaSelecionada = EmpresasFiltro[0];
                AnalistaSelecionado = AnalistasFiltro[0];
                _inicializando = false;
            }

            var mes = (MesSelecionado ?? Meses[0]).Valor;
            var lista = await _checklists.ListarExecucoesAsync(
                mes,
                EmpresaSelecionada is { Id: > 0 } e ? e.Id : null,
                AnalistaSelecionado is { Id: > 0 } a ? a.Id : null);

            var idAnterior = ExecucaoSelecionada?.Id;
            Execucoes.Clear();
            foreach (var x in lista) Execucoes.Add(x);
            ExecucaoSelecionada = Execucoes.FirstOrDefault(x => x.Id == idAnterior);

            Mensagem = $"{Execucoes.Count} checklist(s) na competência";
        }, recarregarNoConflito: false);
    }

    private async Task CarregarItensAsync()
    {
        Itens.Clear();
        var exec = ExecucaoSelecionada;
        if (exec is null)
        {
            TituloDetalhe = "Selecione um checklist à esquerda";
            return;
        }

        try
        {
            TituloDetalhe = $"{exec.EmpresaNome} — {exec.Nome}";
            var itens = await _checklists.ListarItensAsync(exec.Id);
            // Se o usuário trocou a seleção durante a leitura, descarta o resultado antigo.
            if (ExecucaoSelecionada?.Id != exec.Id) return;
            foreach (var i in itens)
                Itens.Add(new ChecklistItemViewModel(i, _checklists, AtualizarProgresso, RecarregarAposFalhaAsync));
        }
        catch (Exception ex)
        {
            Dialogos.Erro("Não foi possível carregar os itens.", ex);
        }
    }

    private void AtualizarProgresso(ChecklistItemViewModel _)
    {
        if (ExecucaoSelecionada is not { } exec) return;
        var concluidos = Itens.Count(i => i.Concluido);
        var indice = Execucoes.IndexOf(exec);
        if (indice < 0) return;

        // Recria a linha para a barra de progresso e o contador se atualizarem na lista.
        Execucoes[indice] = new ChecklistResumo
        {
            Id = exec.Id, EmpresaId = exec.EmpresaId, EmpresaNome = exec.EmpresaNome, AnalistaNome = exec.AnalistaNome,
            Nome = exec.Nome, Competencia = exec.Competencia, Total = exec.Total, Concluidos = concluidos
        };
        ExecucaoSelecionada = Execucoes[indice];
    }

    private async Task RecarregarAposFalhaAsync()
    {
        Dialogos.Aviso("Este item foi alterado por outro usuário ou não pôde ser gravado.\nA tela será atualizada.");
        await CarregarAsync();
        await CarregarItensAsync();
    }

    [RelayCommand]
    private async Task GerarCompetenciaAsync()
    {
        var mes = (MesSelecionado ?? Meses[0]).Valor;
        var competencia = Dialogos.EscolherCompetencia(
            "Gerar checklists",
            "Escolha a competência. Será criado um checklist por empresa ativa, conforme o regime tributário, para os que ainda não existirem.",
            mes);
        if (competencia is null) return;

        await ExecutarAsync(async () =>
        {
            var c = competencia.Value;
            var criados = await _checklists.GerarCompetenciaAsync(c.Year, c.Month);
            Dialogos.Info($"Competência {c:MM/yyyy}: {criados} checklist(s) criado(s).");
            await CarregarAsync();
        });
    }
}
