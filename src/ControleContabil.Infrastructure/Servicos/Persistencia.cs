using ControleContabil.Core;
using Microsoft.EntityFrameworkCore;

namespace ControleContabil.Infrastructure;

internal static class Persistencia
{
    /// <summary>Salva traduzindo conflitos de concorrência do EF para a exceção de domínio.</summary>
    public static async Task SalvarAsync(AppDbContext ctx, CancellationToken ct = default)
    {
        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcorrenciaException();
        }
    }
}
