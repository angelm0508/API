using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class MovimientoInventarioApplicationTests
    {
        private readonly Mock<IMovimientoInventarioDomain> _domain = new();
        private readonly Mock<INumeracionDocumentoDomain> _numeracion = new();
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly MovimientoInventarioApplication _app;

        public MovimientoInventarioApplicationTests()
        {
            _app = new MovimientoInventarioApplication(_domain.Object, _mapper, _numeracion.Object);
        }

        [Fact]
        public async Task ObtenerPorArticuloAsync_PoblaTipoDocNombreConAliasOFallback()
        {
            _domain.Setup(d => d.ObtenerPorArticuloAsync("ART1", null, null, null))
                .ReturnsAsync(new[]
                {
                    new MovimientoInventario { Entry = 1, TipoDoc = "5",  CodArticulo = "ART1", CodAlmacen = "01" },
                    new MovimientoInventario { Entry = 2, TipoDoc = "12", CodArticulo = "ART1", CodAlmacen = "01" },
                    new MovimientoInventario { Entry = 3, TipoDoc = "99", CodArticulo = "ART1", CodAlmacen = "01" },
                });
            _numeracion.Setup(n => n.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new NumeracionDocumento { CodigoObj = "5",  SubTipoDoc = "--", DocAlias = "Entrega" },
                new NumeracionDocumento { CodigoObj = "12", SubTipoDoc = "--", DocAlias = null },
                new NumeracionDocumento { CodigoObj = "5",  SubTipoDoc = "X",  DocAlias = "NO USAR" },
            }.AsAsyncQueryable());

            var r = await _app.ObtenerPorArticuloAsync("ART1", null, null, null);

            var lista = System.Linq.Enumerable.ToList(r.Dato!);
            Assert.Equal("Entrega", lista[0].TipoDocNombre); // alias de la fila '--'
            Assert.Equal("12", lista[1].TipoDocNombre);      // alias nulo -> fallback al código
            Assert.Equal("99", lista[2].TipoDocNombre);      // sin fila -> fallback al código
        }
    }
}
