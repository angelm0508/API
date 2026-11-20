using API.Domain.Core.Pos;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Domain.Interface.Pos;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PosCatalogoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<PosConfiguracion, int>> _repoConfig = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoSerie = new();
        private readonly Mock<IRepositorioGenerico<Almacen, string>> _repoAlmacen = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repoFormaPago = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _repoExistencia = new();
        private readonly Mock<IPrecioListaDomain> _precios = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _facturas = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _detalles = new();
        private readonly PosDomain _domain;

        /// <summary>Precio base por código; la lista 3 vale 10 más (el resolvedor simulado devuelve 40 y 50 para el artículo normal).</summary>
        private readonly Dictionary<string, decimal> _precioBase = new();

        public PosCatalogoDomainTests()
        {
            _precios.Setup(p => p.ResolverAsync(It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync((string codigo, int? lista) =>
                {
                    var baseP = _precioBase.TryGetValue(codigo, out var v) ? v : 40m;
                    var neto = baseP > 0m && lista == 3 ? baseP + 10m : baseP;
                    return new PrecioResuelto(codigo, baseP, 1m, neto);
                });

            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<Factura>().AsAsyncQueryable());
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<FacturaDetalle>().AsAsyncQueryable());
            _repoExistencia.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<ExistenciaArticulo>().AsAsyncQueryable());
            _repoArticulo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(Array.Empty<Articulo>().AsAsyncQueryable());

            _domain = new PosDomain(_repoConfig.Object, _repoSerie.Object, _repoAlmacen.Object, _repoImpuesto.Object,
                _repoSocio.Object, _repoFormaPago.Object, _repoMoneda.Object,
                _repoArticulo.Object, _repoExistencia.Object, _precios.Object,
                new Mock<IFacturaDomain>().Object,
                new Mock<IPagoRecibidoDomain>().Object,
                new Mock<IEjecutorTransaccion>().Object,
                _facturas.Object,
                _detalles.Object,
                new Mock<IRepositorioGenerico<PagoRecibido, int>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>>().Object,
                new Mock<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>>().Object,
                new Mock<IBloqueoSeries>().Object,
                new Mock<IRepositorioGenerico<NumeracionDocumento, string>>().Object);
        }

        private void Articulos(params Articulo[] articulos) =>
            _repoArticulo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(articulos.AsAsyncQueryable());

        private void Existencias(params ExistenciaArticulo[] filas) =>
            _repoExistencia.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

        private void Config(string? almacen = "ALM1", string? clienteDefecto = null) =>
            _repoConfig.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new PosConfiguracion { Id = 1, CodAlmacen = almacen, ClienteDefecto = clienteDefecto });

        private static Articulo Art(string codigo, string nombre, string? barras = null, string activo = "S", string venta = "S",
            string inventario = "S", string pos = "N") => new()
        {
            Codigo = codigo, Nombre = nombre, CodigoBarras = barras, Activo = activo, ArticuloVenta = venta,
            ArticuloInventario = inventario, MostrarEnPos = pos
        };

        private static string[] Codigos(IReadOnlyList<ArticuloPos> lista) => lista.Select(a => a.Codigo).ToArray();

        // ---------- BuscarArticulosAsync ----------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a")]
        [InlineData(" a ")]
        public async Task Buscar_TextoVacioOCortoDeUnCaracter_DevuelveListaVaciaSinConsultar(string? texto)
        {
            Articulos(Art("A1", "Aceite"));

            var r = await _domain.BuscarArticulosAsync(texto, null);

            Assert.Empty(r);
            _repoArticulo.Verify(a => a.ObtenerTodoAsync(), Times.Never);
        }

        [Fact]
        public async Task Buscar_PorCodigoPorCodigoDeBarrasYPorNombre()
        {
            Config();
            Articulos(
                Art("ACE-01", "Filtro de aire"),
                Art("X100", "Bujía", barras: "7501234500011"),
                Art("Z9", "Aceite sintético 5W30"),
                Art("N0", "Otra cosa"));

            Assert.Equal(new[] { "ACE-01" }, Codigos(await _domain.BuscarArticulosAsync("ACE-0", null)));
            Assert.Equal(new[] { "X100" }, Codigos(await _domain.BuscarArticulosAsync("012345", null)));
            Assert.Equal(new[] { "Z9" }, Codigos(await _domain.BuscarArticulosAsync("sintético", null)));
            // «Ac» está en el nombre de uno; «AC» en el código del otro.
            Assert.Equal(new[] { "Z9" }, Codigos(await _domain.BuscarArticulosAsync("Ac", null)));
            Assert.Equal(new[] { "ACE-01" }, Codigos(await _domain.BuscarArticulosAsync("AC", null)));
        }

        [Fact]
        public async Task Buscar_YPorCodigo_ComparanLasColumnasSinToLower_LaInsensibilidadAMayusculasLaDaLaBase()
        {
            // Las columnas se comparan tal cual (sin ToLower) para que SQL Server use el índice único de CodigoBarras y la
            // llave primaria de Codigo; la intercalación de la base (CI) ignora las mayúsculas. El repositorio en memoria de
            // estas pruebas (LINQ a objetos) SÍ las distingue: aquí «ace-01» no encuentra «ACE-01», en SQL real sí.
            Config();
            Articulos(Art("ACE-01", "Filtro de aire", barras: "ABC-999"));

            Assert.Empty(await _domain.BuscarArticulosAsync("ace-01", null));
            Assert.Null(await _domain.ObtenerArticuloPorCodigoAsync("ace-01", null));
            Assert.Null(await _domain.ObtenerArticuloPorCodigoAsync("abc-999", null));
            Assert.Equal("ACE-01", (await _domain.ObtenerArticuloPorCodigoAsync("ABC-999", null))!.Codigo);
        }

        [Fact]
        public async Task Buscar_LaCoincidenciaExactaDeCodigoOBarrasVaPrimeroYLuegoPorNombre()
        {
            Config();
            Articulos(
                Art("X1", "Alfa AB"),
                Art("AB", "Zeta"),
                Art("X2", "Beta", barras: "AB"),
                Art("X3", "Cobre AB"));

            var r = await _domain.BuscarArticulosAsync("AB", null);

            // Exactos (barras X2 «Beta», código AB «Zeta») por nombre, luego los demás por nombre.
            Assert.Equal(new[] { "X2", "AB", "X1", "X3" }, Codigos(r));
        }

        [Fact]
        public async Task Buscar_OmiteInactivosNoDeVentaYSinPrecio()
        {
            Config();
            _precioBase["SIN"] = 0m;
            Articulos(
                Art("ok1", "Tornillo bueno"),
                Art("INA", "Tornillo inactivo", activo: "N"),
                Art("NOV", "Tornillo no de venta", venta: "N"),
                Art("SIN", "Tornillo sin precio"));

            var r = await _domain.BuscarArticulosAsync("Tornillo", null);

            Assert.Equal(new[] { "ok1" }, Codigos(r));
        }

        [Fact]
        public async Task Buscar_DevuelveComoMaximo20DespuesDeFiltrarLosVendibles()
        {
            Config();
            // 30 con precio y, antes por nombre, 5 sin precio que no deben ocupar lugar en el tope.
            var lista = new List<Articulo>();
            for (var i = 0; i < 5; i++) { lista.Add(Art("S" + i, "Tuerca 0" + i)); _precioBase["S" + i] = 0m; }
            for (var i = 0; i < 30; i++) lista.Add(Art("T" + i, "Tuerca " + (10 + i)));
            Articulos(lista.ToArray());

            var r = await _domain.BuscarArticulosAsync("Tuerca", null);

            Assert.Equal(20, r.Count);
            Assert.Equal("T0", r[0].Codigo);
            Assert.Equal("T19", r[19].Codigo);
            Assert.DoesNotContain(r, a => a.Codigo.StartsWith("S"));
        }

        [Fact]
        public async Task Buscar_ConMuchosCandidatos_ResuelveElPrecioSoloHastaCompletarLos20()
        {
            Config();
            var lista = Enumerable.Range(0, 130).Select(i => Art("P" + i.ToString("000"), "Pieza " + i.ToString("000"))).ToArray();
            Articulos(lista);

            var r = await _domain.BuscarArticulosAsync("Pieza", null);

            Assert.Equal(20, r.Count);
            _precios.Verify(p => p.ResolverAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Exactly(20));
        }

        [Fact]
        public async Task Buscar_ElPrecioEsElDeLaListaDelCliente_YBaseSiNoHayLista()
        {
            Config(clienteDefecto: null);
            _repoSocio.Setup(r => r.ObtenerAsync("C3")).ReturnsAsync(new SocioNegocio { Codigo = "C3", NumLstPrecio = 3 });
            _repoSocio.Setup(r => r.ObtenerAsync("C0")).ReturnsAsync(new SocioNegocio { Codigo = "C0", NumLstPrecio = null });
            Articulos(Art("A1", "Aceite"));

            var conLista = await _domain.BuscarArticulosAsync("Aceite", "C3");
            var sinLista = await _domain.BuscarArticulosAsync("Aceite", "C0");
            var sinCliente = await _domain.BuscarArticulosAsync("Aceite", null);

            Assert.Equal(50m, Assert.Single(conLista).Precio);
            Assert.Equal(40m, Assert.Single(sinLista).Precio);
            Assert.Equal(40m, Assert.Single(sinCliente).Precio);
            _precios.Verify(p => p.ResolverAsync("A1", 3), Times.Once);
            _precios.Verify(p => p.ResolverAsync("A1", null), Times.Exactly(2));
        }

        [Fact]
        public async Task Buscar_SinClienteUsaLaListaDelClienteDefectoDeLaConfiguracion()
        {
            Config(clienteDefecto: "C3");
            _repoSocio.Setup(r => r.ObtenerAsync("C3")).ReturnsAsync(new SocioNegocio { Codigo = "C3", NumLstPrecio = 3 });
            Articulos(Art("A1", "Aceite"));

            var r = await _domain.BuscarArticulosAsync("Aceite", "  ");

            Assert.Equal(50m, Assert.Single(r).Precio);
        }

        [Fact]
        public async Task Buscar_ExistenciasDelAlmacenPosYCeroSinFilaOSinAlmacen()
        {
            Config(almacen: "ALM1");
            Articulos(Art("A1", "Aceite uno"), Art("A2", "Aceite dos", inventario: "N"), Art("A3", "Aceite tres"));
            Existencias(
                new ExistenciaArticulo { CodArticulo = "A1", CodAlmacen = "ALM1", Disponible = 12m },
                new ExistenciaArticulo { CodArticulo = "A1", CodAlmacen = "ALM2", Disponible = 99m },
                new ExistenciaArticulo { CodArticulo = "A3", CodAlmacen = "ALM2", Disponible = 7m });

            var r = await _domain.BuscarArticulosAsync("Aceite", null);

            // Por nombre: dos, tres, uno.
            Assert.Equal(new ArticuloPos("A2", null, "Aceite dos", 40m, 0m, false), r[0]);
            Assert.Equal(new ArticuloPos("A3", null, "Aceite tres", 40m, 0m, true), r[1]);
            Assert.Equal(new ArticuloPos("A1", null, "Aceite uno", 40m, 12m, true), r[2]);

            Config(almacen: null);
            var sinAlmacen = await _domain.BuscarArticulosAsync("Aceite", null);
            Assert.All(sinAlmacen, a => Assert.Equal(0m, a.Existencias));
        }

        // ---------- ObtenerArticuloPorCodigoAsync ----------

        [Fact]
        public async Task PorCodigo_PrefiereElCodigoDeBarrasSobreElCodigo()
        {
            Config();
            Articulos(Art("123", "Por código"), Art("B1", "Por barras", barras: "123"));

            var r = await _domain.ObtenerArticuloPorCodigoAsync("123", null);

            Assert.Equal("B1", r!.Codigo);
            Assert.Equal("123", r.CodigoBarras);
        }

        [Fact]
        public async Task PorCodigo_CaeAlCodigoYExigeCoincidenciaExacta()
        {
            Config();
            Articulos(Art("ABC-1", "Uno"), Art("ABC-10", "Diez"));

            Assert.Equal("ABC-1", (await _domain.ObtenerArticuloPorCodigoAsync(" ABC-1 ", null))!.Codigo);
            Assert.Null(await _domain.ObtenerArticuloPorCodigoAsync("ABC", null));
        }

        [Theory]
        [InlineData("inactivo")]
        [InlineData("noventa")]
        [InlineData("sinprecio")]
        [InlineData("noexiste")]
        [InlineData("  ")]
        public async Task PorCodigo_NoExisteONoEsVendible_DevuelveNull(string codigo)
        {
            Config();
            _precioBase["sinprecio"] = 0m;
            Articulos(Art("inactivo", "A", activo: "N"), Art("noventa", "B", venta: "N"), Art("sinprecio", "C"));

            Assert.Null(await _domain.ObtenerArticuloPorCodigoAsync(codigo, null));
        }

        [Fact]
        public async Task PorCodigo_TraePrecioDeLaListaDelClienteYExistenciasDelAlmacenPos()
        {
            Config(almacen: "ALM1");
            _repoSocio.Setup(r => r.ObtenerAsync("C3")).ReturnsAsync(new SocioNegocio { Codigo = "C3", NumLstPrecio = 3 });
            Articulos(Art("A1", "Aceite", barras: "999"));
            Existencias(new ExistenciaArticulo { CodArticulo = "A1", CodAlmacen = "ALM1", Disponible = 12m });

            var r = await _domain.ObtenerArticuloPorCodigoAsync(" 999 ", "C3");

            Assert.Equal(new ArticuloPos("A1", "999", "Aceite", 50m, 12m, true), r);
        }

        // ---------- ObtenerRapidosAsync ----------

        [Fact]
        public async Task Rapidos_ConMarcados_DevuelveSoloLosVendiblesMarcadosPorNombreConTopeDe24()
        {
            Config();
            _precioBase["SIN"] = 0m;
            var lista = new List<Articulo>
            {
                Art("NOMARCADO", "AAA no marcado"),
                Art("INA", "AAB inactivo", activo: "N", pos: "S"),
                Art("SIN", "AAC sin precio", pos: "S")
            };
            for (var i = 0; i < 30; i++) lista.Add(Art("R" + i.ToString("00"), "Rápido " + i.ToString("00"), pos: "S"));
            Articulos(lista.ToArray());

            var r = await _domain.ObtenerRapidosAsync();

            Assert.Equal(24, r.Count);
            Assert.Equal("R00", r[0].Codigo);
            Assert.Equal("R23", r[23].Codigo);
            _facturas.Verify(f => f.ObtenerTodoAsync(), Times.Never);
        }

        private void VentasRecientes(params (string articulo, decimal cantidad)[] lineas)
        {
            var hoy = DateTime.Today;
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new Factura { Entry = 1, NumDoc = 1, Serie = 1, Cancelado = "N", DocSubType = "--", FechaDoc = hoy.AddDays(-5), CodigoSn = "C1", NombreSn = "C", MonedaDoc = "GTQ" },
                new Factura { Entry = 2, NumDoc = 2, Serie = 1, Cancelado = "N", DocSubType = "--", FechaDoc = hoy.AddDays(-40), CodigoSn = "C1", NombreSn = "C", MonedaDoc = "GTQ" }
            }.AsAsyncQueryable());

            var detalles = new List<FacturaDetalle>();
            var n = 1;
            foreach (var (articulo, cantidad) in lineas)
                detalles.Add(new FacturaDetalle { Entry = 1, NoLinea = n++, CodArticulo = articulo, Cantidad = cantidad, CantidadBase = cantidad, TotalLinea = cantidad, Impuesto = 0m, PrctjeDesc = 0m });
            // Una venta de hace 40 días no cuenta.
            detalles.Add(new FacturaDetalle { Entry = 2, NoLinea = 1, CodArticulo = "VIEJO", Cantidad = 500m, CantidadBase = 500m, TotalLinea = 500m, Impuesto = 0m, PrctjeDesc = 0m });
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
        }

        [Fact]
        public async Task Rapidos_SinMarcados_RespaldoDeLosMasVendidosPorCantidadYSoloVendibles()
        {
            Config();
            VentasRecientes(("A", 10m), ("B", 30m), ("C", 20m), ("D", 100m), ("A", 0m));
            Articulos(
                Art("A", "Art A"), Art("B", "Art B"), Art("C", "Art C"),
                Art("D", "Art D inactivo", activo: "N"),
                Art("VIEJO", "Art viejo"));

            var r = await _domain.ObtenerRapidosAsync();

            Assert.Equal(new[] { "B", "C", "A" }, Codigos(r));
        }

        [Fact]
        public async Task Rapidos_SinMarcados_RespaldoSumaPorArticuloYDesempataPorCodigoConTopeDe12()
        {
            Config();
            var lineas = new List<(string, decimal)> { ("Z1", 5m), ("Z1", 5m) };
            for (var i = 0; i < 14; i++) lineas.Add(("K" + i.ToString("00"), 4m));
            VentasRecientes(lineas.ToArray());
            Articulos(lineas.Select(l => l.Item1).Distinct().Select(c => Art(c, "Art " + c)).ToArray());

            var r = await _domain.ObtenerRapidosAsync();

            Assert.Equal(12, r.Count);
            Assert.Equal("Z1", r[0].Codigo);   // 10 = 5 + 5
            Assert.Equal("K00", r[1].Codigo);  // empate en 4: por código
            Assert.Equal("K10", r[11].Codigo);
        }

        [Fact]
        public async Task Rapidos_SinMarcadosNiVentas_DevuelveListaVacia()
        {
            Config();
            Articulos(Art("A", "Art A"));

            Assert.Empty(await _domain.ObtenerRapidosAsync());
        }

        // ---------- Ajustes 5b ----------

        [Fact]
        public async Task Buscar_ElTopeDe100CandidatosEsDuro_SiLos100PrimerosNoTienenPrecioElCandidato101SeIgnora()
        {
            Config();
            // 130 coincidencias por nombre: los 100 primeros (P000..P099) sin precio; P100..P129 sí lo tienen, pero la consulta
            // solo trae 100 filas, así que no se llega a ellos (comportamiento documentado: una búsqueda demasiado amplia no los ve).
            var lista = Enumerable.Range(0, 130).Select(i => Art("P" + i.ToString("000"), "Pieza " + i.ToString("000"))).ToArray();
            foreach (var a in lista.Take(100)) _precioBase[a.Codigo] = 0m;
            Articulos(lista);

            var r = await _domain.BuscarArticulosAsync("Pieza", null);

            Assert.Empty(r);
            _precios.Verify(p => p.ResolverAsync(It.IsAny<string>(), It.IsAny<int?>()), Times.Exactly(100));
            _precios.Verify(p => p.ResolverAsync("P100", It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task Rapidos_RespaldoMasVendidos_SoloConsideraLos50CodigosMasVendidos()
        {
            Config();
            // 60 artículos vendidos con cantidades decrecientes (M00 el que más). La consulta agrupada trae solo los 50
            // primeros (M00..M49) y de ellos solo M10 es vendible: M50..M59 no se consideran (es una comodidad, no un catálogo).
            var lineas = Enumerable.Range(0, 60).Select(i => ("M" + i.ToString("00"), (decimal)(100 - i))).ToArray();
            VentasRecientes(lineas);
            Articulos(lineas.Select(l => Art(l.Item1, "Art " + l.Item1)).ToArray());
            for (var i = 0; i < 50; i++)
                if (i != 10) _precioBase["M" + i.ToString("00")] = 0m;

            var r = await _domain.ObtenerRapidosAsync();

            Assert.Equal(new[] { "M10" }, Codigos(r));
            _precios.Verify(p => p.ResolverAsync("M50", It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task Rapidos_RespaldoMasVendidos_SoloFacturasYNotasDeDebitoNoCanceladasDeLosUltimos30Dias()
        {
            Config();
            var hoy = DateTime.Today;
            Factura F(int entry, DateTime fecha, string cancelado = "N", string subTipo = "--") =>
                new() { Entry = entry, NumDoc = entry, Serie = 1, Cancelado = cancelado, DocSubType = subTipo, FechaDoc = fecha, CodigoSn = "C1", NombreSn = "C", MonedaDoc = "GTQ" };
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                F(1, hoy),                              // hoy: cuenta
                F(2, hoy.AddDays(-30)),                 // límite de 30 días: cuenta
                F(3, hoy.AddDays(-31)),                 // fuera del período
                F(4, hoy.AddDays(-1), cancelado: "S"),  // cancelada
                F(5, hoy.AddDays(-2), subTipo: "ND"),   // nota de débito: cuenta
                F(6, hoy.AddDays(-2), subTipo: "XX"),   // otro subtipo
                F(7, hoy.AddDays(1)),                   // mañana: fuera (HastaExclusiva = hoy + 1)
                new Factura { Entry = 8, NumDoc = 8, Serie = 1, Cancelado = null, DocSubType = "--", FechaDoc = hoy, CodigoSn = "C1", NombreSn = "C", MonedaDoc = "GTQ" }
            }.AsAsyncQueryable());
            FacturaDetalle D(int entry, string articulo, decimal? cantidad, decimal? cantidadBase) =>
                new() { Entry = entry, NoLinea = 1, CodArticulo = articulo, Cantidad = cantidad, CantidadBase = cantidadBase, TotalLinea = 1m, Impuesto = 0m, PrctjeDesc = 0m };
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                D(1, "HOY", 10m, 10m),
                D(2, "LIMITE", 30m, 30m),
                D(3, "VIEJO", 900m, 900m),
                D(4, "CANCELADO", 900m, 900m),
                D(5, "ND", 3m, 20m),        // cuenta la cantidad base (20), no la de la línea
                D(6, "OTRO", 900m, 900m),
                D(7, "FUTURO", 900m, 900m),
                D(8, "SINBASE", 5m, null)   // sin cantidad base usa la cantidad (y Cancelado nulo cuenta)
            }.AsAsyncQueryable());
            Articulos(new[] { "HOY", "LIMITE", "VIEJO", "CANCELADO", "ND", "OTRO", "FUTURO", "SINBASE" }.Select(c => Art(c, "Art " + c)).ToArray());

            var r = await _domain.ObtenerRapidosAsync();

            Assert.Equal(new[] { "LIMITE", "ND", "HOY", "SINBASE" }, Codigos(r));
        }

        [Fact]
        public async Task Rapidos_ExistenciasCeroSinFilaEnElAlmacenPos()
        {
            Config(almacen: "ALM1");
            Articulos(Art("A1", "Aceite", pos: "S"), Art("A2", "Bujía", pos: "S"));
            Existencias(
                new ExistenciaArticulo { CodArticulo = "A1", CodAlmacen = "ALM1", Disponible = 8m },
                new ExistenciaArticulo { CodArticulo = "A2", CodAlmacen = "ALM2", Disponible = 99m });

            var r = await _domain.ObtenerRapidosAsync();

            Assert.Equal(8m, r.Single(a => a.Codigo == "A1").Existencias);
            Assert.Equal(0m, r.Single(a => a.Codigo == "A2").Existencias);
        }

        [Fact]
        public async Task PorCodigo_SinFilaDeExistenciasEnElAlmacenPos_DevuelveExistenciasCero()
        {
            Config(almacen: "ALM1");
            Articulos(Art("A1", "Aceite", barras: "999"));
            Existencias(new ExistenciaArticulo { CodArticulo = "A1", CodAlmacen = "ALM2", Disponible = 50m });

            var r = await _domain.ObtenerArticuloPorCodigoAsync("999", null);

            Assert.Equal(0m, r!.Existencias);
        }

        [Fact]
        public async Task PorCodigo_SiDosArticulosCompartenBarras_ElegidoEsElDeMenorCodigo()
        {
            Config();
            // Datos antiguos con barras repetidas: el resultado no debe depender del orden físico.
            Articulos(Art("Z9", "Zeta", barras: "777"), Art("B2", "Beta", barras: "777"), Art("M5", "Mu", barras: "777"));

            var r = await _domain.ObtenerArticuloPorCodigoAsync("777", null);

            Assert.Equal("B2", r!.Codigo);
        }

        [Fact]
        public async Task Rapidos_ConMarcadosPeroNingunoVendible_TambienUsaElRespaldoDeMasVendidos()
        {
            Config();
            _precioBase["MARC"] = 0m;
            VentasRecientes(("V1", 5m), ("V2", 9m));
            Articulos(Art("MARC", "Marcado sin precio", pos: "S"), Art("V1", "Vendido uno"), Art("V2", "Vendido dos"));

            var r = await _domain.ObtenerRapidosAsync();

            // Comportamiento actual: marcados no vendibles no impiden el respaldo.
            Assert.Equal(new[] { "V2", "V1" }, Codigos(r));
        }

        [Fact]
        public async Task Rapidos_ConMasDe200MilLineasEnElPeriodo_IgualDevuelveLosMasVendidos()
        {
            // El respaldo ya no usa la consulta de líneas de los reportes (tope de 200,000 líneas): agrupa en SQL.
            Config();
            var hoy = DateTime.Today;
            _facturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new Factura { Entry = 1, NumDoc = 1, Serie = 1, Cancelado = "N", DocSubType = "--", FechaDoc = hoy.AddDays(-5), CodigoSn = "C1", NombreSn = "C", MonedaDoc = "GTQ" }
            }.AsAsyncQueryable());
            var detalles = new List<FacturaDetalle>(200_001);
            for (var i = 1; i <= 200_001; i++)
                detalles.Add(new FacturaDetalle { Entry = 1, NoLinea = i, CodArticulo = "A", Cantidad = 1m, CantidadBase = 1m, TotalLinea = 1m, Impuesto = 0m, PrctjeDesc = 0m });
            _detalles.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(detalles.AsAsyncQueryable());
            Articulos(Art("A", "Art A"));

            var r = await _domain.ObtenerRapidosAsync();

            Assert.Equal(new[] { "A" }, Codigos(r));
        }
    }
}
