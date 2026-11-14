using API.Domain.Interface;
using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Saldos por cobrar (C) o por pagar (P) repartidos por días de vencimiento, al día de hoy: reutiliza
    /// <see cref="IAntiguedadService"/> (sin modificarlo; el servicio no admite fecha de corte). Una fila por socio,
    /// ordenada por total descendente (desempate por código).
    /// </summary>
    public sealed class AntiguedadSaldosReporte : IReporte
    {
        public const string CodigoReporte = "antiguedad-saldos";

        private readonly IAntiguedadService _antiguedad;

        public AntiguedadSaldosReporte(IAntiguedadService antiguedad)
        {
            _antiguedad = antiguedad;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Antigüedad de saldos",
            Descripcion: "Saldos por cobrar o por pagar repartidos por días de vencimiento, al día de hoy.",
            Familia: "Cobros y pagos",
            Modulo: "ReportesCobrosPagos",
            Filtros: new[]
            {
                new FiltroReporte("tipo", "Tipo", TipoFiltro.Lista, Obligatorio: true, Opciones: new[]
                {
                    new OpcionFiltro("C", "Por cobrar (clientes)"),
                    new OpcionFiltro("P", "Por pagar (proveedores)")
                }, ValorPorDefecto: "C"),
                new FiltroReporte("socio", "Código de socio", TipoFiltro.Texto)
            },
            Columnas: new[]
            {
                new ColumnaReporte("codigoSn", "Código", TipoColumna.Texto),
                new ColumnaReporte("nombreSn", "Socio", TipoColumna.Texto),
                new ColumnaReporte("vigente", "Corriente", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("de1a30", "1–30 días", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("de31a60", "31–60", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("de61a90", "61–90", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("mas90", "Más de 90", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var tipo = filtros.Texto("tipo") ?? "C";
            var socio = filtros.Texto("socio");

            IEnumerable<FilaAntiguedad> filas = await _antiguedad.ObtenerAsync(tipo);
            if (socio is not null) filas = filas.Where(f => f.CodigoSn == socio);

            return filas
                .OrderByDescending(f => f.Total)
                .ThenBy(f => f.CodigoSn, StringComparer.Ordinal)
                .Take(limite)
                .Select(f => new FilaReporte
                {
                    ["codigoSn"] = f.CodigoSn,
                    ["nombreSn"] = f.NombreSn,
                    ["vigente"] = f.Vigente,
                    ["de1a30"] = f.De1a30,
                    ["de31a60"] = f.De31a60,
                    ["de61a90"] = f.De61a90,
                    ["mas90"] = f.Mas90,
                    ["total"] = f.Total
                })
                .ToList();
        }
    }
}
