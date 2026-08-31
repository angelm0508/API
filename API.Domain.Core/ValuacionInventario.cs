using API.Domain.Interface;

namespace API.Domain.Core
{
    public class ValuacionInventario : IValuacionInventario
    {
        private const string PromedioMovil = "P";

        public ResultadoValuacion CalcularEntrada(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad, decimal precioUnitario)
        {
            if (metodo == PromedioMovil)
            {
                var total = cantActual + cantidad;
                var nuevoCosto = total == 0m
                    ? costoPromActual
                    : (cantActual * costoPromActual + cantidad * precioUnitario) / total;
                return new ResultadoValuacion(
                    NuevoCostoPromedio: nuevoCosto,
                    CostoUnitarioMov: precioUnitario,
                    ValorMovimiento: cantidad * precioUnitario,
                    VariacionPrecio: 0m);
            }

            // Estándar: el stock siempre se valúa al costo estándar; la diferencia va a variación.
            return new ResultadoValuacion(
                NuevoCostoPromedio: costoEstandar,
                CostoUnitarioMov: costoEstandar,
                ValorMovimiento: cantidad * costoEstandar,
                VariacionPrecio: cantidad * (precioUnitario - costoEstandar));
        }

        public ResultadoValuacion CalcularSalida(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad)
        {
            var costo = metodo == PromedioMovil ? costoPromActual : costoEstandar;
            return new ResultadoValuacion(
                NuevoCostoPromedio: costo,       // la salida no recalcula el promedio
                CostoUnitarioMov: costo,
                ValorMovimiento: -cantidad * costo,
                VariacionPrecio: 0m);
        }
    }
}
