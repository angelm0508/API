using API.Domain.Interface;
using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Estado de cuenta de un cliente o proveedor: reutiliza <see cref="IEstadoCuentaService"/> (sin modificarlo).
    /// Primera fila «Saldo inicial» y luego un renglón por movimiento con su saldo acumulado. Los documentos
    /// informativos ya vienen con cargo y abono en cero. Si el servicio rechaza la solicitud (socio inexistente,
    /// fechas invertidas) se relanza como <see cref="ReporteInvalidoException"/> con el MISMO mensaje, para que el
    /// usuario vea un 400 con ese texto.
    /// </summary>
    public sealed class EstadoCuentaSocioReporte : IReporte
    {
        public const string CodigoReporte = "estado-cuenta-socio";

        private readonly IEstadoCuentaService _estadoCuenta;

        public EstadoCuentaSocioReporte(IEstadoCuentaService estadoCuenta)
        {
            _estadoCuenta = estadoCuenta;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Estado de cuenta de un socio",
            Descripcion: "Cargos, abonos y saldo de un cliente o proveedor.",
            Familia: "Cobros y pagos",
            Modulo: "ReportesCobrosPagos",
            Filtros: new[]
            {
                new FiltroReporte("socio", "Socio", TipoFiltro.Socio, Obligatorio: true),
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha),
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("descripcion", "Descripción", TipoColumna.Texto),
                new ColumnaReporte("cargo", "Cargo", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("abono", "Abono", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("saldo", "Saldo", TipoColumna.Dinero)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var socio = filtros.Texto("socio") ?? "";
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas")?.AddDays(-1);   // el servicio recibe el día final inclusive

            EstadoCuenta estado;
            try
            {
                estado = await _estadoCuenta.ObtenerAsync(socio, desde, hasta);
            }
            catch (Exception ex)
            {
                throw new ReporteInvalidoException(ex.Message);
            }

            var filas = new List<FilaReporte>(estado.Movimientos.Count + 1)
            {
                new()
                {
                    ["fecha"] = estado.Desde,
                    ["tipo"] = "Saldo inicial",
                    ["numDoc"] = null,
                    ["descripcion"] = "",
                    ["cargo"] = 0m,
                    ["abono"] = 0m,
                    ["saldo"] = estado.SaldoInicial
                }
            };
            filas.AddRange(estado.Movimientos.Select(m => new FilaReporte
            {
                ["fecha"] = m.Fecha,
                ["tipo"] = m.Tipo,
                ["numDoc"] = m.NumDoc,
                ["descripcion"] = m.Descripcion,
                ["cargo"] = m.Cargo,
                ["abono"] = m.Abono,
                ["saldo"] = m.SaldoAcumulado
            }));

            return filas.Take(limite).ToList();
        }
    }
}
