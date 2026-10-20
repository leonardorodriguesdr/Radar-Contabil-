using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;
using ControleContabil.Infrastructure;

namespace ControleContabil.Desktop.ViewModels;

public record AndamentoAnalista(string Nome, int Total, int Finalizadas, int Atrasadas)
{
    public double Percentual => Total == 0 ? 0 : Math.Round(100.0 * Finalizadas / Total, 0);
    public string Resumo => $"{Finalizadas}/{Total} finalizadas" + (Atrasadas > 0 ? $" · {Atrasadas} atrasada(s)" : "");
}

public partial class DashboardViewModel : PaginaBase
{
    private readonly ObrigacaoService _obrigacoes;
    private readonly UsuarioService _usuarios;
    private readonly DispatcherTimer _timer;
    private bool _suprimirRecarga;

    public DashboardViewModel(ObrigacaoService obrigacoes, UsuarioService usuarios, IDialogoService dialogos) : base(dialogos)
    {
        _obrigacoes = obrigacoes;
        _usuarios = usuarios;

        var hoje = DataHora.Hoje();
        _mesSelecionado = Meses.First(m => m.Valor.Year == hoje.Year && m.Valor.Month == hoje.Month);

        // Vários usuários usam o sistema ao mesmo tempo: o painel se atualiza sozinho a cada 2 minutos.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
        _timer.Tick += (_, _) => _ = CarregarAsync();
        _timer.Start();
    }

    public override string Titulo => "Painel de acompanhamento";

    public List<MesItem> Meses { get; } = Opcoes.Meses(6, 3);

    [ObservableProperty] private MesItem? _mesSelecionado;
    [ObservableProperty] private List<Usuario> _analistas = new();
    [ObservableProperty] private Usuario? _analistaSelecionado;

    [ObservableProperty] private int _total;
    [ObservableProperty] private int _finalizadas;
    [ObservableProperty] private int _emDia;
    [ObservableProperty] private int _proximas;
    [ObservableProperty] private int _atrasadas;
    [ObservableProperty] private double _percentualFinalizado;

    public ObservableCollection<ObrigacaoItem> Atencao { get; } = new();
    public ObservableCollection<AndamentoAnalista> PorAnalista { get; } = new();

    partial void OnMesSelecionadoChanged(MesItem? value)
    {
        if (!_suprimirRecarga) _ = CarregarAsync();
    }

    partial void OnAnalistaSelecionadoChanged(Usuario? value)
    {
        if (!_suprimirRecarga) _ = CarregarAsync();
    }

    public override async Task CarregarAsync()
    {
        await ExecutarAsync(async () =>
        {
            if (Analistas.Count == 0)
            {
                var lista = await _usuarios.ListarAsync();
                _suprimirRecarga = true;
                Analistas = new[] { new Usuario { Id = 0, Nome = "Todos os analistas" } }.Concat(lista).ToList();
                AnalistaSelecionado = Analistas[0];
                _suprimirRecarga = false;
            }

            var mes = (MesSelecionado ?? Meses[0]).Valor;
            int? responsavel = AnalistaSelecionado is { Id: > 0 } a ? a.Id : null;

            var itens = await _obrigacoes.ListarAsync(new ObrigacaoFiltro
            {
                VencimentoDe = mes,
                VencimentoAte = mes.AddMonths(1).AddDays(-1),
                IncluirAtrasadasAnteriores = true,
                ResponsavelId = responsavel
            });

            Total = itens.Count;
            Finalizadas = itens.Count(i => i.Situacao == SituacaoPrazo.Concluida);
            EmDia = itens.Count(i => i.Situacao == SituacaoPrazo.EmDia);
            Proximas = itens.Count(i => i.Situacao == SituacaoPrazo.ProximoVencimento);
            Atrasadas = itens.Count(i => i.Situacao == SituacaoPrazo.Atrasada);
            PercentualFinalizado = Total == 0 ? 0 : Math.Round(100.0 * Finalizadas / Total, 0);

            Atencao.Clear();
            foreach (var i in itens
                         .Where(i => i.Situacao is SituacaoPrazo.Atrasada or SituacaoPrazo.ProximoVencimento)
                         .OrderBy(i => i.Vencimento).Take(80))
                Atencao.Add(i);

            PorAnalista.Clear();
            foreach (var g in itens.GroupBy(i => i.ResponsavelNome).OrderBy(g => g.Key))
                PorAnalista.Add(new AndamentoAnalista(
                    g.Key,
                    g.Count(),
                    g.Count(i => i.Situacao == SituacaoPrazo.Concluida),
                    g.Count(i => i.Situacao == SituacaoPrazo.Atrasada)));

            Mensagem = $"Atualizado às {DateTime.Now:HH:mm}";
        }, recarregarNoConflito: false);
    }

    [RelayCommand]
    private Task AtualizarAsync() => CarregarAsync();

    public override void Dispose()
    {
        _timer.Stop();
        base.Dispose();
    }
}
