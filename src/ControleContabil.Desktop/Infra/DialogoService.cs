using System.Diagnostics;
using System.IO;
using System.Windows;
using ControleContabil.Core;
using Microsoft.Win32;
using Janelas = ControleContabil.Desktop.Windows;

namespace ControleContabil.Desktop.Infra;

/// <summary>Abstrai caixas de diálogo e janelas modais para que os ViewModels não dependam de WPF diretamente.</summary>
public interface IDialogoService
{
    void Info(string mensagem);
    void Aviso(string mensagem);
    void Erro(string mensagem, Exception? ex = null);
    bool Confirmar(string mensagem, string titulo = "Confirmação");

    Empresa? EditarEmpresa(Empresa empresa, IReadOnlyList<Usuario> usuarios);
    Usuario? EditarUsuario(Usuario usuario);
    ObrigacaoModelo? EditarModeloObrigacao(ObrigacaoModelo modelo);
    (ChecklistModelo Modelo, List<string> Itens)? EditarModeloChecklist(ChecklistModelo modelo, IReadOnlyList<Empresa> empresas);
    ObrigacaoEdicao? EditarObrigacao(ObrigacaoEdicao dados, IReadOnlyList<Empresa> empresas, IReadOnlyList<Usuario> usuarios);
    DateOnly? EscolherCompetencia(string titulo, string mensagem, DateOnly sugestao);

    IReadOnlyList<string> EscolherArquivos();
    string? EscolherArquivoParaSalvar(string nomeSugerido);
    void Abrir(string caminho);
    void MostrarNaPasta(string caminho);
}

public class DialogoService : IDialogoService
{
    private static Window? Dono => Application.Current?.MainWindow;

    public void Info(string mensagem) =>
        MessageBox.Show(mensagem, "Radar Contábil", MessageBoxButton.OK, MessageBoxImage.Information);

    public void Aviso(string mensagem) =>
        MessageBox.Show(mensagem, "Radar Contábil", MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Erro(string mensagem, Exception? ex = null) =>
        MessageBox.Show(ex is null ? mensagem : $"{mensagem}\n\n{ex.Message}", "Radar Contábil",
            MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirmar(string mensagem, string titulo = "Confirmação") =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public Empresa? EditarEmpresa(Empresa empresa, IReadOnlyList<Usuario> usuarios)
    {
        var w = new Janelas.EmpresaEditWindow(empresa, usuarios) { Owner = Dono };
        return w.ShowDialog() == true ? w.Resultado : null;
    }

    public Usuario? EditarUsuario(Usuario usuario)
    {
        var w = new Janelas.UsuarioEditWindow(usuario) { Owner = Dono };
        return w.ShowDialog() == true ? w.Resultado : null;
    }

    public ObrigacaoModelo? EditarModeloObrigacao(ObrigacaoModelo modelo)
    {
        var w = new Janelas.ModeloObrigacaoEditWindow(modelo) { Owner = Dono };
        return w.ShowDialog() == true ? w.Resultado : null;
    }

    public (ChecklistModelo Modelo, List<string> Itens)? EditarModeloChecklist(ChecklistModelo modelo, IReadOnlyList<Empresa> empresas)
    {
        var w = new Janelas.ModeloChecklistEditWindow(modelo, empresas) { Owner = Dono };
        return w.ShowDialog() == true && w.Resultado is { } resultado ? (resultado, w.Itens) : null;
    }

    public ObrigacaoEdicao? EditarObrigacao(ObrigacaoEdicao dados, IReadOnlyList<Empresa> empresas, IReadOnlyList<Usuario> usuarios)
    {
        var w = new Janelas.ObrigacaoEditWindow(dados, empresas, usuarios) { Owner = Dono };
        return w.ShowDialog() == true ? w.Resultado : null;
    }

    public DateOnly? EscolherCompetencia(string titulo, string mensagem, DateOnly sugestao)
    {
        var w = new Janelas.CompetenciaWindow(titulo, mensagem, sugestao) { Owner = Dono };
        return w.ShowDialog() == true ? w.Resultado : null;
    }

    public IReadOnlyList<string> EscolherArquivos()
    {
        var dlg = new OpenFileDialog { Multiselect = true, Title = "Selecione os documentos", CheckFileExists = true };
        return dlg.ShowDialog(Dono) == true ? dlg.FileNames : Array.Empty<string>();
    }

    public string? EscolherArquivoParaSalvar(string nomeSugerido)
    {
        var dlg = new SaveFileDialog
        {
            FileName = nomeSugerido,
            Filter = "CSV (Excel) (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true
        };
        return dlg.ShowDialog(Dono) == true ? dlg.FileName : null;
    }

    public void Abrir(string caminho)
    {
        if (!File.Exists(caminho))
        {
            Aviso("O arquivo não foi encontrado no repositório de documentos.");
            return;
        }
        Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
    }

    public void MostrarNaPasta(string caminho)
    {
        if (File.Exists(caminho))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{caminho}\"") { UseShellExecute = true });
        else if (Directory.Exists(Path.GetDirectoryName(caminho)))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.GetDirectoryName(caminho)}\"") { UseShellExecute = true });
        else
            Aviso("A pasta do documento não foi encontrada.");
    }
}
