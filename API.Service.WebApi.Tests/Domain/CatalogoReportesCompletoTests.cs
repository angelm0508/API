using API.Domain.Core.Reportes;
using API.Domain.Interface.Reportes;
using System.Runtime.CompilerServices;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// Coherencia del catálogo completo (fase 4): instancia TODAS las clases IReporte de Domain.Core con dependencias simuladas
    /// y verifica códigos, familias, columnas, filtros y el registro en Startup.
    /// </summary>
    public class CatalogoReportesCompletoTests
    {
        private static readonly string[] CodigosEsperados =
        {
            "ventas-por-articulo", "articulos-mas-vendidos", "utilidad-por-periodo", "ventas-por-cliente", "articulos-sin-ventas",
            "notas-credito-debito", "documentos-venta-estado", "stock-bajo-minimo", "kardex-articulo", "movimientos-inventario",
            "facturas-por-cobrar", "facturas-por-pagar", "pagos-recibidos", "pagos-efectuados", "estado-cuenta-socio",
            "antiguedad-saldos", "compras-por-documento", "compras-por-articulo", "documentos-compra-estado", "historial-precios-compra",
            "ventas-por-documento", "existencias-valor"
        };

        private static readonly (string Familia, string Modulo)[] FamiliasValidas =
        {
            ("Ventas", "ReportesVentas"),
            ("Inventario", "ReportesInventario"),
            ("Cobros y pagos", "ReportesCobrosPagos"),
            ("Compras", "ReportesCompras")
        };

        private static IReadOnlyList<Type> TiposReporte() =>
            typeof(CatalogoReportes).Assembly.GetTypes()
                .Where(t => typeof(IReporte).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

        private static IReadOnlyList<IReporte> TodosLosReportes() =>
            TiposReporte().Select(t => (IReporte)Construir(t)).ToList();

        // Interfaces -> Mock.Of<T>(); clases concretas (p. ej. ConsultaLineasVenta, sellada) -> se construyen recursivamente.
        private static object Construir(Type tipo)
        {
            if (tipo.IsInterface)
            {
                var of = typeof(Mock).GetMethods()
                    .First(m => m.Name == "Of" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                return of.MakeGenericMethod(tipo).Invoke(null, null)!;
            }

            var ctor = tipo.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            var args = ctor.GetParameters().Select(p => Construir(p.ParameterType)).ToArray();
            return ctor.Invoke(args);
        }

        // Sube desde el directorio de las pruebas y, si la salida está fuera del repo (BaseOutputPath), desde este archivo fuente.
        private static string RaizDelRepositorio([CallerFilePath] string archivoFuente = "")
        {
            foreach (var inicio in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(archivoFuente) ?? "" })
            {
                var dir = string.IsNullOrEmpty(inicio) ? null : new DirectoryInfo(inicio);
                while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "API.sln"))) dir = dir.Parent;
                if (dir is not null) return dir.FullName;
            }
            throw new InvalidOperationException("No se encontró API.sln subiendo desde el directorio de las pruebas.");
        }

        [Fact]
        public void Los_22_codigos_esperados_existen_y_son_unicos()
        {
            var codigos = TodosLosReportes().Select(r => r.Definicion.Codigo).ToList();

            Assert.Equal(CodigosEsperados.Length, CodigosEsperados.Distinct().Count());
            Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(CodigosEsperados.OrderBy(c => c, StringComparer.Ordinal), codigos.OrderBy(c => c, StringComparer.Ordinal));
        }

        [Fact]
        public void El_catalogo_se_construye_con_todos_los_reportes_y_los_busca_por_codigo()
        {
            var catalogo = new CatalogoReportes(TodosLosReportes());

            Assert.Equal(22, catalogo.Todos.Count);
            foreach (var codigo in CodigosEsperados) Assert.NotNull(catalogo.Buscar(codigo));
        }

        [Fact]
        public void Cada_reporte_pertenece_a_una_de_las_cuatro_familias_con_su_modulo()
        {
            foreach (var r in TodosLosReportes())
                Assert.True(FamiliasValidas.Contains((r.Definicion.Familia, r.Definicion.Modulo)),
                    $"{r.Definicion.Codigo}: familia/módulo inválidos ({r.Definicion.Familia}/{r.Definicion.Modulo}).");
        }

        [Fact]
        public void Todo_reporte_tiene_al_menos_una_columna_visible_por_defecto()
        {
            foreach (var r in TodosLosReportes())
                Assert.True(r.Definicion.Columnas.Any(c => c.VisiblePorDefecto),
                    $"{r.Definicion.Codigo}: no tiene columnas visibles por defecto.");
        }

        [Fact]
        public void Los_campos_de_columnas_y_de_filtros_son_unicos_dentro_de_cada_reporte()
        {
            foreach (var r in TodosLosReportes())
            {
                var columnas = r.Definicion.Columnas.Select(c => c.Campo).ToList();
                var filtros = r.Definicion.Filtros.Select(f => f.Campo).ToList();
                Assert.True(columnas.Count == columnas.Distinct(StringComparer.Ordinal).Count(), $"{r.Definicion.Codigo}: columnas con Campo repetido.");
                Assert.True(filtros.Count == filtros.Distinct(StringComparer.Ordinal).Count(), $"{r.Definicion.Codigo}: filtros con Campo repetido.");
            }
        }

        [Fact]
        public void Todo_reporte_con_dinero_totalizado_y_columna_moneda_la_deja_oculta_por_defecto()
        {
            // El motor aplica la regla multimoneda sobre `moneda`; mostrarla por defecto ensuciaría los reportes de un solo QTZ.
            foreach (var r in TodosLosReportes())
            {
                var tieneDineroConSuma = r.Definicion.Columnas.Any(c => c.Tipo == TipoColumna.Dinero && c.Totaliza == Totalizacion.Suma);
                var moneda = r.Definicion.Columnas.SingleOrDefault(c => c.Campo == "moneda");
                if (tieneDineroConSuma && moneda is not null)
                    Assert.False(moneda.VisiblePorDefecto, $"{r.Definicion.Codigo}: la columna 'moneda' debe tener VisiblePorDefecto = false.");
            }
        }

        [Fact]
        public void Todas_las_clases_IReporte_de_Domain_Core_estan_registradas_en_Startup()
        {
            var startup = File.ReadAllText(Path.Combine(RaizDelRepositorio(), "API.Service.WebApi", "Startup.cs"));
            var tipos = TiposReporte();

            Assert.Equal(22, tipos.Count);
            foreach (var t in tipos)
                Assert.True(startup.Contains($"AddTransient<IReporte, {t.Name}>"), $"{t.Name} no está registrada en Startup.cs.");
        }
    }
}
