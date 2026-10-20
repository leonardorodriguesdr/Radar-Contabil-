using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

public class UsuarioService
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;

    public UsuarioService(IDbContextFactory<AppDbContext> fabrica) => _fabrica = fabrica;

    public async Task<List<Usuario>> ListarAsync(bool apenasAtivos = true, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var q = ctx.Usuarios.AsNoTracking();
        if (apenasAtivos) q = q.Where(u => u.Ativo);
        return await q.OrderBy(u => u.Nome).ToListAsync(ct);
    }

    public async Task<Usuario?> ObterPorLoginAsync(string login, CancellationToken ct = default)
    {
        await using var ctx = await _fabrica.CreateDbContextAsync(ct);
        var l = login.ToLower();
        return await ctx.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Ativo && u.LoginWindows != null && u.LoginWindows.ToLower() == l, ct);
    }

    public async Task SalvarAsync(Usuario u, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(u.Nome)) throw new ValidacaoException("Informe o nome do usuário.");
        u.Nome = u.Nome.Trim();
        u.LoginWindows = string.IsNullOrWhiteSpace(u.LoginWindows) ? null : u.LoginWindows.Trim();
        u.Email = string.IsNullOrWhiteSpace(u.Email) ? null : u.Email.Trim();

        await using var ctx = await _fabrica.CreateDbContextAsync(ct);

        if (u.LoginWindows is { } login)
        {
            var l = login.ToLower();
            var duplicado = await ctx.Usuarios.AnyAsync(x => x.Id != u.Id && x.LoginWindows != null && x.LoginWindows.ToLower() == l, ct);
            if (duplicado) throw new ValidacaoException("Já existe um usuário com este login do Windows.");
        }

        if (u.Id == 0)
        {
            ctx.Usuarios.Add(u);
        }
        else
        {
            var atual = await ctx.Usuarios.FindAsync(new object[] { u.Id }, ct)
                        ?? throw new ValidacaoException("Usuário não encontrado.");
            if (atual.Versao != u.Versao) throw new ConcorrenciaException();

            if (!u.Ativo || u.Perfil == PerfilUsuario.Analista)
            {
                var outrosAdmins = await ctx.Usuarios.CountAsync(
                    x => x.Id != u.Id && x.Ativo && x.Perfil >= PerfilUsuario.Coordenador, ct);
                if (outrosAdmins == 0 && atual.Perfil >= PerfilUsuario.Coordenador)
                    throw new ValidacaoException("É necessário manter ao menos um coordenador/administrador ativo.");
            }

            ctx.Entry(atual).CurrentValues.SetValues(u);
        }

        await Persistencia.SalvarAsync(ctx, ct);
    }
}
