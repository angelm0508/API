using API.Domain.Core.Pos;
using API.Domain.Interface.Pos;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PosCalculadoraTests
    {
        private static VentaPosPago Pago(string forma, decimal monto) => new(forma, monto, null, null, null);

        // ---- Líneas ----

        [Fact]
        public void Linea_ImpuestoSobreBruto()
        {
            var l = PosCalculadora.Linea("A1", 2, 45.00m, 12m);
            Assert.Equal(90.00m, l.Bruto);
            Assert.Equal(10.80m, l.Impuesto);
            Assert.Equal(100.80m, l.TotalLinea);
            Assert.Equal("A1", l.CodArticulo);
            Assert.Equal(2, l.Cantidad);
            Assert.Equal(45.00m, l.PrecioNeto);
        }

        [Fact]
        public void Linea_ImpuestoSobreBrutoSinRedondear()
        {
            var l = PosCalculadora.Linea("A2", 3, 33.333m, 12m);
            Assert.Equal(100.00m, l.Bruto);
            Assert.Equal(12.00m, l.Impuesto); // 11.99988 redondeado
            Assert.Equal(112.00m, l.TotalLinea);
        }

        [Fact]
        public void Linea_MedioRedondeaHaciaArriba()
        {
            var l = PosCalculadora.Linea("A3", 1, 0.005m, 0m);
            Assert.Equal(0.01m, l.Bruto);
        }

        [Fact]
        public void Linea_TasaCero_SinImpuesto()
        {
            var l = PosCalculadora.Linea("A4", 4, 10m, 0m);
            Assert.Equal(40m, l.Bruto);
            Assert.Equal(0m, l.Impuesto);
            Assert.Equal(40m, l.TotalLinea);
        }

        // ---- Totales ----

        [Fact]
        public void Totales_SumaBrutoImpuestoYTotal()
        {
            var lineas = new List<LineaPosCalculada>
            {
                PosCalculadora.Linea("A", 2, 45m, 12m),
                PosCalculadora.Linea("B", 1, 22m, 12m)
            };
            var t = PosCalculadora.Totales(lineas);
            Assert.Equal(112.00m, t.Bruto);
            Assert.Equal(13.44m, t.Impuesto);
            Assert.Equal(125.44m, t.Total);
            Assert.Equal(2, t.Lineas.Count);
        }

        // ---- Pagos ----

        [Fact]
        public void ValidarPagos_EfectivoConCambio()
        {
            var r = PosCalculadora.ValidarPagos(112.00m, new[] { Pago("EFE", 200m) }, "EFE");
            Assert.Equal(200m, r.Pagado);
            Assert.Equal(88.00m, r.Cambio);
            var m = Assert.Single(r.Medios);
            Assert.Equal("EFE", m.CodFormaPago);
            Assert.Equal(112.00m, m.Monto);
        }

        [Fact]
        public void ValidarPagos_Mixto_ConservaOrdenYRestaCambioDelEfectivo()
        {
            var r = PosCalculadora.ValidarPagos(112.00m, new[] { Pago("TJT", 50m), Pago("EFE", 100m) }, "EFE");
            Assert.Equal(38.00m, r.Cambio);
            Assert.Equal(2, r.Medios.Count);
            Assert.Equal("TJT", r.Medios[0].CodFormaPago);
            Assert.Equal(50.00m, r.Medios[0].Monto);
            Assert.Equal("EFE", r.Medios[1].CodFormaPago);
            Assert.Equal(62.00m, r.Medios[1].Monto);
        }

        [Fact]
        public void ValidarPagos_Exacto_SinCambio()
        {
            var r = PosCalculadora.ValidarPagos(112.00m, new[] { Pago("EFE", 112m) }, "EFE");
            Assert.Equal(0m, r.Cambio);
            var m = Assert.Single(r.Medios);
            Assert.Equal(112.00m, m.Monto);
        }

        [Fact]
        public void ValidarPagos_DosEfectivos_PrimeroQuedaEnCeroYSeDescarta()
        {
            var r = PosCalculadora.ValidarPagos(100.00m, new[] { Pago("EFE", 10m), Pago("EFE", 200m) }, "EFE");
            Assert.Equal(110.00m, r.Cambio);
            var m = Assert.Single(r.Medios);
            Assert.Equal(100.00m, m.Monto);
        }

        [Fact]
        public void ValidarPagos_ConservaDatosDelCheque()
        {
            var fecha = new DateTime(2026, 11, 19);
            var pago = new VentaPosPago("CHQ", 112m, "123", "Banco X", fecha);
            var r = PosCalculadora.ValidarPagos(112m, new[] { pago }, "EFE");
            var m = Assert.Single(r.Medios);
            Assert.Equal("123", m.Referencia);
            Assert.Equal("Banco X", m.Banco);
            Assert.Equal(fecha, m.FechaCheque);
        }

        [Fact]
        public void ValidarPagos_SinPagos_Error()
        {
            var ex = Assert.Throws<Exception>(() => PosCalculadora.ValidarPagos(112m, new List<VentaPosPago>(), "EFE"));
            Assert.Equal("Indica al menos una forma de pago.", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void ValidarPagos_FormaVacia_Error(string forma)
        {
            var ex = Assert.Throws<Exception>(() => PosCalculadora.ValidarPagos(112m, new[] { Pago(forma, 112m) }, "EFE"));
            Assert.Equal("La forma de pago es requerida.", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void ValidarPagos_MontoCeroONegativo_Error(int monto)
        {
            var ex = Assert.Throws<Exception>(() => PosCalculadora.ValidarPagos(112m, new[] { Pago("EFE", monto) }, "EFE"));
            Assert.Equal("El monto de cada forma de pago debe ser mayor que cero.", ex.Message);
        }

        [Fact]
        public void ValidarPagos_PagoNoCubreTotal_Error()
        {
            var ex = Assert.Throws<Exception>(() => PosCalculadora.ValidarPagos(112m, new[] { Pago("EFE", 100m) }, "EFE"));
            Assert.Equal("El pago (100.00) no cubre el total (112.00).", ex.Message);
        }

        [Fact]
        public void ValidarPagos_CambioConTarjeta_Error()
        {
            var ex = Assert.Throws<Exception>(() => PosCalculadora.ValidarPagos(112m, new[] { Pago("TJT", 150m) }, "EFE"));
            Assert.Equal("El cambio (38.00) solo puede darse con efectivo.", ex.Message);
        }

        [Fact]
        public void ValidarPagos_SinFormaEfectivoConfigurada_NoHayCambio()
        {
            var ex = Assert.Throws<Exception>(() => PosCalculadora.ValidarPagos(112m, new[] { Pago("TJT", 150m) }, null));
            Assert.Equal("El cambio (38.00) solo puede darse con efectivo.", ex.Message);
        }

        [Fact]
        public void ValidarPagos_SinFormaEfectivoConfigurada_PagoExacto_Ok()
        {
            var r = PosCalculadora.ValidarPagos(112m, new[] { Pago("TJT", 112m) }, null);
            Assert.Equal(0m, r.Cambio);
            var m = Assert.Single(r.Medios);
            Assert.Equal(112m, m.Monto);
        }

        // ---- Marca POS ----

        [Fact]
        public void PosMarca_Comentario()
        {
            Assert.Equal("Venta POS - Cajero: ana", PosMarca.Comentario("ana"));
        }

        [Fact]
        public void PosMarca_EsVentaPos()
        {
            Assert.True(PosMarca.EsVentaPos("Venta POS - Cajero: ana"));
            Assert.False(PosMarca.EsVentaPos("otro"));
            Assert.False(PosMarca.EsVentaPos("venta pos - cajero: ana"));
            Assert.False(PosMarca.EsVentaPos(null));
        }

        [Fact]
        public void PosMarca_Cajero()
        {
            Assert.Equal("ana", PosMarca.Cajero("Venta POS - Cajero: ana"));
            Assert.Equal(string.Empty, PosMarca.Cajero("otro"));
            Assert.Equal(string.Empty, PosMarca.Cajero(null));
        }
    }
}
