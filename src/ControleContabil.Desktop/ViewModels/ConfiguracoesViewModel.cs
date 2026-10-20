using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;
using ControleContabil.Infrastructure;

namespace ControleContabil.Desktop.ViewModels;

public partial class ConfiguracoesViewModel : PaginaBase
{
    private readonly UsuarioService _usuarios;
    private readonly ObrigacaoService _obrigacoes;
    private readonly ChecklistService _checklists;
    private readonly EmpresaService _empresas;

    public ConfiguracoesViewModel(UsuarioService usuarios, ObrigacaoService obrigacoes, ChecklistService checklists,
        EmpresaService empresas, IDialogoService dialogos) : base(dialogos)
    {
        _usuarios = usuarios;
        _obrigacoes = obrigacoes;
        _checklists = checklists;
        _empresas = empresas;
    }

    public override string Titulo => "Configurações";

    public ObservableCollection<Usuario> Usuarios { get; } = new();
    public ObservableCollection<ObrigacaoModelo> ModelosObrigacao { get; } = new();
    public ObservableCollection<ChecklistModelo> ModelosChecklist { get; } = new();

    [ObservableProperty] private Usuario? _usuarioSelecionado;
    [ObservableProperty] private ObrigacaoModelo? _modeloObrigacaoSelecionado;
    [ObservableProperty] private ChecklistModelo? _modeloChecklistSelecionado;

    public override async Task CarregarAsync()
    {
        await ExecutarAsync(async () =>
        {
            var u = await _usuarios.ListarAsync(apenasAtivos: false);
            var mo = await _obrigacoes.ListarModelosAsync();
            var mc = await _checklists.ListarModelosAsync();

            Troca(Usuarios, u);
            Troca(ModelosObrigacao, mo);
            Troca(ModelosChecklist, mc);
        }, recarregarNoConflito: false);
    }

    private static void Troca<T>(ObservableCollection<T> destino, IEnumerable<T> origem)
    {
        destino.Clear();
        foreach (var x in origem) destino.Add(x);
    }

    // ----- Usuários -----

    [RelayCommand]
    private async Task NovoUsuarioAsync()
    {
        var r = Dialogos.EditarUsuario(new Usuario());
        if (r is null) return;
        await ExecutarAsync(async () => { await _usuarios.SalvarAsync(r); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task EditarUsuarioAsync()
    {
        if (UsuarioSelecionado is not { } sel) return;
        var copia = new Usuario
        {
            Id = sel.Id, Versao = sel.Versao, Nome = sel.Nome, Email = sel.Email,
            LoginWindows = sel.LoginWindows, Perfil = sel.Perfil, Ativo = sel.Ativo
        };
        var r = Dialogos.EditarUsuario(copia);
        if (r is null) return;
        await ExecutarAsync(async () => { await _usuarios.SalvarAsync(r); await CarregarAsync(); });
    }

    // ----- Modelos de obrigação -----

    [RelayCommand]
    private async Task NovoModeloObrigacaoAsync()
    {
        var r = Dialogos.EditarModeloObrigacao(new ObrigacaoModelo());
        if (r is null) return;
        await ExecutarAsync(async () => { await _obrigacoes.SalvarModeloAsync(r); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task EditarModeloObrigacaoAsync()
    {
        if (ModeloObrigacaoSelecionado is not { } sel) return;
        var copia = new ObrigacaoModelo
        {
            Id = sel.Id, Versao = sel.Versao, Nome = sel.Nome, Descricao = sel.Descricao, Tipo = sel.Tipo,
            Periodicidade = sel.Periodicidade, Regra = sel.Regra, Dia = sel.Dia,
            MesesAposCompetencia = sel.MesesAposCompetencia, MesVencimento = sel.MesVencimento,
            AntecipaDiaNaoUtil = sel.AntecipaDiaNaoUtil, RegimesAplicaveis = sel.RegimesAplicaveis, Ativo = sel.Ativo
        };
        var r = Dialogos.EditarModeloObrigacao(copia);
        if (r is null) return;
        await ExecutarAsync(async () => { await _obrigacoes.SalvarModeloAsync(r); await CarregarAsync(); });
    }

    // ----- Modelos de checklist -----

    [RelayCommand]
    private async Task NovoModeloChecklistAsync()
    {
        var empresas = await _empresas.ListarAsync();
        var r = Dialogos.EditarModeloChecklist(new ChecklistModelo(), empresas);
        if (r is null) return;
        await ExecutarAsync(async () => { await _checklists.SalvarModeloAsync(r.Value.Modelo, r.Value.Itens); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task EditarModeloChecklistAsync()
    {
        if (ModeloChecklistSelecionado is not { } sel) return;
        var empresas = await _empresas.ListarAsync();
        var copia = new ChecklistModelo
        {
            Id = sel.Id, Versao = sel.Versao, Nome = sel.Nome, RegimesAplicaveis = sel.RegimesAplicaveis,
            EmpresaId = sel.EmpresaId, Ativo = sel.Ativo,
            Itens = sel.Itens.Select(i => new ChecklistModeloItem { Ordem = i.Ordem, Descricao = i.Descricao }).ToList()
        };
        var r = Dialogos.EditarModeloChecklist(copia, empresas);
        if (r is null) return;
        await ExecutarAsync(async () => { await _checklists.SalvarModeloAsync(r.Value.Modelo, r.Value.Itens); await CarregarAsync(); });
    }
}
