namespace API.Domain.Interface
{
    /// <summary>Resultado de valuar un movimiento de inventario a nivel artículo.</summary>
    public record ResultadoValuacion(
        decimal NuevoCostoPromedio,
        decimal CostoUnitarioMov,
        decimal ValorMovimiento,
        decimal VariacionPrecio);

    /// <summary>
    /// Cálculo de costos de inventario. Función pura: no toca base de datos ni estado.
    /// Métodos soportados: "P" = promedio móvil, cualquier otro valor = estándar.
    /// </summary>
    public interface IValuacionInventario
    {
        ResultadoValuacion CalcularEntrada(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad, decimal precioUnitario);

        ResultadoValuacion CalcularSalida(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad);
    }
}
