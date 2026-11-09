using System.Text.Json.Serialization;

namespace API.Domain.Interface.Reportes
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoColumna { Texto, Entero, Cantidad, Dinero, Fecha }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Totalizacion { Ninguna, Suma, Promedio, Conteo }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoFiltro { Fecha, RangoFechas, Texto, Numero, Lista, Socio, Articulo, Almacen }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ParteFecha { Dia, Mes, Anio }

    public sealed record OpcionFiltro(string Valor, string Titulo);

    public sealed record ColumnaReporte(
        string Campo,
        string Titulo,
        TipoColumna Tipo,
        bool Agrupable = false,
        Totalizacion Totaliza = Totalizacion.Ninguna,
        bool VisiblePorDefecto = true);

    /// <summary>SubTipo: en filtros de tipo Socio, "C" = solo clientes, "P" = solo proveedores, null = ambos.</summary>
    public sealed record FiltroReporte(
        string Campo,
        string Titulo,
        TipoFiltro Tipo,
        bool Obligatorio = false,
        IReadOnlyList<OpcionFiltro>? Opciones = null,
        string? ValorPorDefecto = null,
        string? SubTipo = null);

    public sealed record ReporteDefinicion(
        string Codigo,
        string Nombre,
        string Descripcion,
        string Familia,
        string Modulo,
        IReadOnlyList<FiltroReporte> Filtros,
        IReadOnlyList<ColumnaReporte> Columnas);
}
