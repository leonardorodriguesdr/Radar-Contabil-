using System.Security.Cryptography;
using System.Text;
using ControleContabil.Core;

namespace ControleContabil.Infrastructure;

/// <summary>Abstração do repositório de arquivos. A implementação padrão grava em pasta local/de rede;
/// para S3, Azure Blob, SharePoint etc. basta criar outra implementação e registrar no lugar.</summary>
public interface IArmazenamentoDocumentos
{
    /// <summary>Copia o arquivo para o repositório e devolve o caminho relativo e o hash SHA-256.</summary>
    Task<(string CaminhoRelativo, string Hash, long Tamanho)> SalvarAsync(
        string cnpj, DateOnly competencia, string arquivoOrigem, CancellationToken ct = default);

    string CaminhoCompleto(string caminhoRelativo);
    bool Existe(string caminhoRelativo);
    Task RemoverAsync(string caminhoRelativo, CancellationToken ct = default);
}

public class ArmazenamentoLocal : IArmazenamentoDocumentos
{
    private readonly string _raiz;

    public ArmazenamentoLocal(ArmazenamentoOptions opcoes)
    {
        var raiz = string.IsNullOrWhiteSpace(opcoes.RaizDocumentos)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ControleContabil", "Documentos")
            : opcoes.RaizDocumentos;
        _raiz = Path.GetFullPath(raiz);
    }

    public async Task<(string CaminhoRelativo, string Hash, long Tamanho)> SalvarAsync(
        string cnpj, DateOnly competencia, string arquivoOrigem, CancellationToken ct = default)
    {
        if (!File.Exists(arquivoOrigem)) throw new ValidacaoException("O arquivo selecionado não foi encontrado.");

        var pasta = Path.Combine(CnpjValidator.Normalizar(cnpj), competencia.ToString("yyyy-MM"));
        var nome = NomeSeguro(Path.GetFileName(arquivoOrigem));
        var relativo = Path.Combine(pasta, nome);

        // Evita sobrescrever: acrescenta um sufixo se já existir.
        var contador = 1;
        while (File.Exists(CaminhoCompleto(relativo)))
        {
            relativo = Path.Combine(pasta, $"{Path.GetFileNameWithoutExtension(nome)} ({++contador}){Path.GetExtension(nome)}");
        }

        var destino = CaminhoCompleto(relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        await using (var origem = File.OpenRead(arquivoOrigem))
        await using (var saida = File.Create(destino))
        {
            await origem.CopyToAsync(saida, ct);
        }

        string hash;
        await using (var lido = File.OpenRead(destino))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(lido, ct));
        }

        return (relativo, hash, new FileInfo(destino).Length);
    }

    public string CaminhoCompleto(string caminhoRelativo)
    {
        var completo = Path.GetFullPath(Path.Combine(_raiz, caminhoRelativo));
        // Garante que nada escape da raiz configurada (path traversal).
        if (!completo.StartsWith(_raiz, StringComparison.OrdinalIgnoreCase))
            throw new ValidacaoException("Caminho de documento inválido.");
        return completo;
    }

    public bool Existe(string caminhoRelativo) => File.Exists(CaminhoCompleto(caminhoRelativo));

    public Task RemoverAsync(string caminhoRelativo, CancellationToken ct = default)
    {
        var caminho = CaminhoCompleto(caminhoRelativo);
        if (File.Exists(caminho)) File.Delete(caminho);
        return Task.CompletedTask;
    }

    private static string NomeSeguro(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in nome) sb.Append(invalidos.Contains(c) ? '_' : c);
        var r = sb.ToString().Trim();
        return r.Length == 0 ? "arquivo" : r;
    }
}
