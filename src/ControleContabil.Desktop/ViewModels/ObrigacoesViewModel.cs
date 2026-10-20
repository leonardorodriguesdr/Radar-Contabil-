using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;
using ControleContabil.Infrastructure;

namespace ControleContabil.Desktop.ViewModels;

public partial class ObrigacoesViewModel : PaginaBase
{
    private readonly ObrigacaoService _obrigacoes;
    private readonly EmpresaService _empresas;
    private readonly UsuarioService _usuarios;
    private readonly UsuarioAtual _atual;
    private bool _inicializando = true;
    private int _sequencia;

    public ObrigacoesViewModel(ObrigacaoService obrigacoes, EmpresaService empresas, UsuarioService usuarios,
        UsuarioAtual atual, IDialogoService dialogos) : base(dialogos)
    {
        _obrigacoes = obrigacoes;
        _empresas = empresas;
        _usuarios = usuarios;
        _atual = atual;

        var hoje = DataHora.Hoje();
        _mesSelecionado = Meses.First(m => m.Valor.Year == hoje.Year && m.Valor.Month == hoje.Month);
        _situacaoSelecionada = Opcoes.Situacoes[0];
    }

    public override string Titulo => "Obrigações e prazos";

    public bool PodeAdministrar => _atual.PodeAdministrar;

    public List<MesItem> Meses { get; } = Opcoes.Meses(12, 6);
    public IReadOnlyList<Opcao<SituacaoPrazo?>> Situacoes => Opcoes.Situacoes;

    public ObservableCollection<ObrigacaoItem> Itens { get; } = new();

    /// <summary>Linhas marcadas no DataGrid (seleção múltipla), preenchida pela View.</summary>
    public IList? Selecionados { get; set; }

    [ObservableProperty] private List<Empresa> _empresasFiltro = new();
    [ObservableProperty] private List<Usuario> _responsaveisFiltro = new();

    [ObservableProperty] private MesItem? _mesSelecionado;
    [ObservableProperty] private Opcao<SituacaoPrazo?> _situacaoSelecionada;
    [ObservableProperty] private Empresa? _empresaSelecionada;
    [ObservableProperty] private Usuario? _responsavelSelecionado;
    [ObservableProperty] private string _texto = "";
    [ObservableProperty] private bool _ocultarFinalizadas;
    [ObservableProperty] private bool _todosOsMeses;

    partial void OnMesSelecionadoChanged(MesItem? value) => Recarregar();
    partial void OnSituacaoSelecionadaChanged(Opcao<SituacaoPrazo?> value) => Recarregar();
    partial void OnEmpresaSelecionadaChanged(Empresa? value) => Recarregar();
    partial void OnResponsavelSelecionadoChanged(Usuario? value) => Recarregar();
    partial void OnTextoChanged(string value) => Recarregar();
    partial void OnOcultarFinalizadasChanged(bool value) => Recarregar();
    partial void OnTodosOsMesesChanged(bool value) => Recarregar();

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
                ResponsaveisFiltro = new[] { new Usuario { Id = 0, Nome = "Todos os responsáveis" } }.Concat(usuarios).ToList();
                EmpresaSelecionada = EmpresasFiltro[0];
                ResponsavelSelecionado = ResponsaveisFiltro[0];
                _inicializando = false;
            }

            var token = ++_sequencia;
            var filtro = new ObrigacaoFiltro
            {
                EmpresaId = EmpresaSelecionada is { Id: > 0 } e ? e.Id : null,
                ResponsavelId = ResponsavelSelecionado is { Id: > 0 } r ? r.Id : null,
                Situacao = SituacaoSelecionada.Valor,
                OcultarFinalizadas = OcultarFinalizadas,
                Texto = Texto
            };
            if (!TodosOsMeses && MesSelecionado is { } m)
            {
                filtro.VencimentoDe = m.Valor;
                filtro.VencimentoAte = m.Valor.AddMonths(1).AddDays(-1);
                filtro.IncluirAtrasadasAnteriores = true;
            }

            var lista = await _obrigacoes.ListarAsync(filtro);
            if (token != _sequencia) return;

            Itens.Clear();
            foreach (var i in lista) Itens.Add(i);

            var atrasadas = lista.Count(i => i.Situacao == SituacaoPrazo.Atrasada);
            Mensagem = $"{lista.Count} obrigação(ões) · {atrasadas} atrasada(s)";
        }, recarregarNoConflito: false);
    }

    private List<ObrigacaoItem> SelecaoAtual() =>
        (Selecionados ?? Array.Empty<object>()).Cast<ObrigacaoItem>().ToList();

    [RelayCommand]
    private async Task IniciarAsync()
    {
        var sel = SelecaoAtual().Where(i => i.Aberta && i.Status != StatusObrigacao.EmAndamento).ToList();
        if (sel.Count == 0) { Dialogos.Info("Selecione ao menos uma obrigação pendente."); return; }
        await ExecutarAsync(async () => { await _obrigacoes.IniciarAsync(sel); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task ConcluirAsync()
    {
        var sel = SelecaoAtual().Where(i => i.Aberta).ToList();
        if (sel.Count == 0) { Dialogos.Info("Selecione ao menos uma obrigação em aberto."); return; }
        if (sel.Count > 1 && !Dialogos.Confirmar($"Marcar {sel.Count} obrigações como concluídas?")) return;
        await ExecutarAsync(async () => { await _obrigacoes.ConcluirAsync(sel); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task DispensarAsync()
    {
        var sel = SelecaoAtual().Where(i => i.Aberta).ToList();
        if (sel.Count == 0) { Dialogos.Info("Selecione ao menos uma obrigação em aberto."); return; }
        if (!Dialogos.Confirmar($"Dispensar {sel.Count} obrigação(ões)? Use quando a obrigação não se aplica nesta competência.")) return;
        await ExecutarAsync(async () => { await _obrigacoes.DispensarAsync(sel); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task ReabrirAsync()
    {
        var sel = SelecaoAtual().Where(i => !i.Aberta).ToList();
        if (sel.Count == 0) { Dialogos.Info("Selecione ao menos uma obrigação concluída ou dispensada."); return; }
        if (!Dialogos.Confirmar($"Reabrir {sel.Count} obrigação(ões)?")) return;
        await ExecutarAsync(async () => { await _obrigacoes.ReabrirAsync(sel); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task NovaAsync()
    {
        var empresas = await _empresas.ListarAsync();
        var usuarios = await _usuarios.ListarAsync();
        var dados = Dialogos.EditarObrigacao(new ObrigacaoEdicao(), empresas, usuarios);
        if (dados is null) return;
        await ExecutarAsync(async () => { await _obrigacoes.SalvarAsync(dados); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task EditarAsync()
    {
        var sel = SelecaoAtual();
        if (sel.Count != 1) { Dialogos.Info("Selecione uma única obrigação para editar."); return; }

        var atual = await _obrigacoes.ObterParaEdicaoAsync(sel[0].Id);
        if (atual is null) { Dialogos.Aviso("A obrigação não existe mais."); await CarregarAsync(); return; }

        var empresas = await _empresas.ListarAsync(incluirInativas: true);
        var usuarios = await _usuarios.ListarAsync();
        var dados = Dialogos.EditarObrigacao(atual, empresas, usuarios);
        if (dados is null) return;
        await ExecutarAsync(async () => { await _obrigacoes.SalvarAsync(dados); await CarregarAsync(); });
    }

    [RelayCommand]
    private async Task GerarCompetenciaAsync()
    {
        var proximo = DataHora.Hoje();
        var sugestao = new DateOnly(proximo.Year, proximo.Month, 1).AddMonths(-1);
        var competencia = Dialogos.EscolherCompetencia(
            "Gerar obrigações",
            "Escolha a competência (mês de referência). Serão criadas as obrigações e os checklists das empresas ativas que ainda não existirem.",
            sugestao);
        if (competencia is null) return;

        await ExecutarAsync(async () =>
        {
            var c = competencia.Value;
            var obrigacoes = await _obrigacoes.GerarCompetenciaAsync(c.Year, c.Month);
            Dialogos.Info($"Competência {c:MM/yyyy}: {obrigacoes} obrigação(ões) criada(s).\n" +
                          "Para criar os checklists, use a tela Checklists → Gerar da competência.");
            await CarregarAsync();
        });
    }

    [RelayCommand]
    private void ExportarCsv()
    {
        var caminho = CaminhoCsv("obrigacoes");
        if (string.IsNullOrEmpty(caminho)) return;
        try
        {
            CsvExport.Salvar(caminho, Itens,
                ("Empresa", i => i.EmpresaNome),
                ("CNPJ", i => i.EmpresaCnpj),
                ("Regime", i => i.RegimeTexto),
                ("Obrigação", i => i.Nome),
                ("Tipo", i => i.TipoTexto),
                ("Competência", i => i.CompetenciaTexto),
                ("Vencimento", i => i.Vencimento),
                ("Situação", i => i.SituacaoTexto),
                ("Status", i => i.StatusTexto),
                ("Responsável", i => i.ResponsavelNome));
            Dialogos.Info("Arquivo exportado com sucesso.");
        }
        catch (Exception ex)
        {
            Dialogos.Erro("Não foi possível gravar o arquivo.", ex);
        }
    }
}
