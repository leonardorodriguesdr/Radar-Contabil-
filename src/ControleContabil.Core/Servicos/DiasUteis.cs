using System.Collections.Concurrent;

namespace ControleContabil.Core;

/// <summary>Calendário de dias úteis. Considera fins de semana, feriados nacionais e os dias sem
/// expediente bancário (Carnaval e Corpus Christi). Feriados estaduais/municipais NÃO são considerados.</summary>
public static class DiasUteis
{
    private static readonly ConcurrentDictionary<int, HashSet<DateOnly>> Cache = new();

    public static IReadOnlySet<DateOnly> Feriados(int ano) => Cache.GetOrAdd(ano, Calcular);

    public static bool EhUtil(DateOnly d) =>
        d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday && !Feriados(d.Year).Contains(d);

    public static DateOnly NesimoDiaUtil(int ano, int mes, int n)
    {
        if (n < 1) n = 1;
        var d = new DateOnly(ano, mes, 1);
        var contados = 0;
        while (true)
        {
            if (EhUtil(d) && ++contados == n) return d;
            d = d.AddDays(1);
        }
    }

    public static DateOnly UltimoDiaUtil(int ano, int mes)
    {
        var d = new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes));
        while (!EhUtil(d)) d = d.AddDays(-1);
        return d;
    }

    /// <summary>Se a data não for dia útil, move para o dia útil anterior (antecipar) ou seguinte (prorrogar).</summary>
    public static DateOnly Ajustar(DateOnly d, bool antecipar)
    {
        while (!EhUtil(d)) d = d.AddDays(antecipar ? -1 : 1);
        return d;
    }

    private static HashSet<DateOnly> Calcular(int ano)
    {
        var pascoa = Pascoa(ano);
        return new HashSet<DateOnly>
        {
            new(ano, 1, 1),    // Confraternização Universal
            new(ano, 4, 21),   // Tiradentes
            new(ano, 5, 1),    // Dia do Trabalho
            new(ano, 9, 7),    // Independência
            new(ano, 10, 12),  // N. Sra. Aparecida
            new(ano, 11, 2),   // Finados
            new(ano, 11, 15),  // Proclamação da República
            new(ano, 11, 20),  // Consciência Negra (nacional desde 2024)
            new(ano, 12, 25),  // Natal
            pascoa.AddDays(-48), // Segunda de Carnaval (sem expediente bancário)
            pascoa.AddDays(-47), // Terça de Carnaval (sem expediente bancário)
            pascoa.AddDays(-2),  // Sexta-feira Santa
            pascoa.AddDays(60)   // Corpus Christi (sem expediente bancário)
        };
    }

    /// <summary>Algoritmo de Meeus/Jones/Butcher para o domingo de Páscoa.</summary>
    public static DateOnly Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(ano, mes, dia);
    }
}
