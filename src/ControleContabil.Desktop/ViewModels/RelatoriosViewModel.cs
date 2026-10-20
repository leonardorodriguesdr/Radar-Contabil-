using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;
using ControleContabil.Infrastructure;

namespace ControleContabil.Desktop.ViewModels;

public partial class RelatoriosViewModel : PaginaBase
{
    private readonly RelatorioService _relatorios;
    private readonly UsuarioService _usuarios;
    private bool _inicializando = true;

    public RelatoriosViewModel(RelatorioService relatorios, UsuarioService usuarios, IDialogoService dialogos) : base(dialogos)
    {
        _relatorios = relatorios;
        _usuarios = usuarios;

        var hoje = DataHora.Hoje();
        De = new DateOnly(hoje.Year, hoje.Month, 1);
        Ate = De.AddMonths(1).AddDays(-1);
    }

    public override string Titulo => "Relatórios e métricas";

    public ObservableCollection<ProdutividadeLinha> Produtividade { get; } = new();
    public ObservableCollection<PendenciaClienteLinha> Pendencias { get; } = new();
    public ObservableCollection<AuditoriaLinha> Auditoria { get; } = new();

    private DateOnly _dataInicial;
    private DateOnly _dataFinal;

    public DateOnly De
    {
        get => _dataInicial;
        private set
        {
            if (SetProperty(ref _dataInicial, value))
                OnPropertyChanged(nameof(DeDatePicker));
        }
    }

    public DateOnly Ate
    {
        get => _dataFinal;
        private set
        {
            if (SetProperty(ref _dataFinal, value))
                OnPropertyChanged(nameof(AteDatePicker));
        }
    }

    [ObservableProperty] private List<Usuario> _usuariosFiltro = new();
    [ObservableProperty] private Usuario? _usuarioAuditoria;
    [ObservableProperty] private string _textoAuditoria = "";

    /// <summary>DatePicker trabalha com DateTime; estas propriedades fazem a ponte com DateOnly.</summary>
    public DateTime DeDatePicker
    {
        get => De.ToDateTime(TimeOnly.MinValue);
        set => De = DateOnly.FromDateTime(value);
    }

    public DateTime AteDatePicker
    {
        get => Ate.ToDateTime(TimeOnly.MinValue);
        set => Ate = DateOnly.FromDateTime(value);
    }

    public override async Task CarregarAsync()
    {
        await ExecutarAsync(async () =>
        {
            if (_inicializando)
            {
                var usuarios = await _usuarios.ListarAsync(apenasAtivos: false);
                UsuariosFiltro = new[] { new Usuario { Id = 0, Nome = "Todos os usuários" } }.Concat(usuarios).ToList();
                UsuarioAuditoria = UsuariosFiltro[0];
                _inicializando = false;
            }

            if (Ate < De)
                throw new ValidacaoException("A data final deve ser igual ou posterior à data inicial.");

            var prod = await _relatorios.ProdutividadeAsync(De, Ate);
            var pend = await _relatorios.PendenciasPorClienteAsync();
            var aud = await _relatorios.AuditoriaAsync(new AuditoriaFiltro
            {
                De = De,
                Ate = Ate,
                UsuarioId = UsuarioAuditoria is { Id: > 0 } u ? u.Id : null,
                Texto = TextoAuditoria
            });

            Troca(Produtividade, prod);
            Troca(Pendencias, pend);
            Troca(Auditoria, aud);
            Mensagem = $"Período {De:dd/MM/yyyy} a {Ate:dd/MM/yyyy} · auditoria limitada às 1.000 ações mais recentes";
        }, recarregarNoConflito: false);
    }

    private static void Troca<T>(ObservableCollection<T> destino, IEnumerable<T> origem)
    {
        destino.Clear();
        foreach (var x in origem) destino.Add(x);
    }

    [RelayCommand]
    private Task AplicarAsync() => CarregarAsync();

    [RelayCommand]
    private void ExportarProdutividade() => Exportar("produtividade", Produtividade,
        ("Analista", l => l.Analista),
        ("Total no período", l => l.Total),
        ("Concluídas", l => l.Concluidas),
        ("No prazo", l => l.ConcluidasNoPrazo),
        ("Com atraso", l => l.ConcluidasComAtraso),
        ("Em aberto", l => l.EmAberto),
        ("Atrasadas", l => l.Atrasadas),
        ("% conclusão", l => l.PercConclusao),
        ("% no prazo", l => l.PercNoPrazo),
        ("Itens de checklist", l => l.ItensChecklistConcluidos));

    [RelayCommand]
    private void ExportarPendencias() => Exportar("pendencias_por_cliente", Pendencias,
        ("Empresa", l => l.Empresa),
        ("CNPJ", l => l.Cnpj),
        ("Regime", l => l.RegimeTexto),
        ("Analista", l => l.Analista),
        ("Em aberto", l => l.Abertas),
        ("Atrasadas", l => l.Atrasadas),
        ("Próximas do vencimento", l => l.Proximas),
        ("Próximo vencimento", l => l.ProximoVencimento));

    [RelayCommand]
    private void ExportarAuditoria() => Exportar("auditoria", Auditoria,
        ("Data/hora", l => l.DataHoraLocal),
        ("Usuário", l => l.Usuario),
        ("Ação", l => l.Acao),
        ("Entidade", l => l.Entidade),
        ("Descrição", l => l.Descricao),
        ("Empresa", l => l.Empresa),
        ("Detalhes", l => l.Detalhes));

    private void Exportar<T>(string nome, IEnumerable<T> dados, params (string, Func<T, object?>)[] colunas)
    {
        var caminho = CaminhoCsv(nome);
        if (string.IsNullOrEmpty(caminho)) return;
        try
        {
            CsvExport.Salvar(caminho, dados, colunas);
            Dialogos.Info("Arquivo exportado com sucesso.");
        }
        catch (Exception ex)
        {
            Dialogos.Erro("Não foi possível gravar o arquivo.", ex);
        }
    }
}
