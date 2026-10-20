using CommunityToolkit.Mvvm.ComponentModel;
using ControleContabil.Core;
using ControleContabil.Desktop.Infra;

namespace ControleContabil.Desktop.ViewModels;

public abstract partial class PaginaBase : ObservableObject, IDisposable
{
    protected readonly IDialogoService Dialogos;

    protected PaginaBase(IDialogoService dialogos) => Dialogos = dialogos;

    public abstract string Titulo { get; }

    [ObservableProperty]
    private bool _ocupado;

    /// <summary>Texto de status exibido no rodapé da página (contagens, hora da atualização etc.).</summary>
    [ObservableProperty]
    private string _mensagem = "";

    public abstract Task CarregarAsync();

    public virtual void Dispose() { }

    /// <summary>Executa uma operação tratando os erros esperados (validação e concorrência) de forma amigável.</summary>
    protected async Task ExecutarAsync(Func<Task> acao, bool recarregarNoConflito = true)
    {
        try
        {
            Ocupado = true;
            await acao();
        }
        catch (ValidacaoException ex)
        {
            Dialogos.Aviso(ex.Message);
        }
        catch (ConcorrenciaException)
        {
            Dialogos.Aviso("Este registro foi alterado por outro usuário enquanto você trabalhava nele.\nA tela será atualizada; refaça a operação.");
            if (recarregarNoConflito) await CarregarAsync();
        }
        catch (Exception ex)
        {
            Dialogos.Erro("Não foi possível concluir a operação.", ex);
        }
        finally
        {
            Ocupado = false;
        }
    }

    protected string CaminhoCsv(string nomeBase) =>
        Dialogos.EscolherArquivoParaSalvar($"{nomeBase}_{DateTime.Now:yyyyMMdd_HHmm}.csv") ?? "";
}
