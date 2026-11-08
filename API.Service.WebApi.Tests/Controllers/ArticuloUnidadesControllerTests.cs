using API.Application.DTO;
using API.Application.DTO.articulo.unidades;
using API.Application.DTO.factura;
using API.Application.Interface;
using API.Application.Main;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Controllers;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ArticuloUnidadesControllerTests
    {
        private readonly Mock<IUnidadArticuloApplication> _app = new();

        private ArticuloUnidadesController Crear() => new(_app.Object);

        [Fact]
        public async Task Obtener_Ok_DevuelveUnidadesDelArticulo()
        {
            var buena = new Respuesta<UnidadesArticuloDTO>
            {
                Resultado = true,
                Dato = new UnidadesArticuloDTO
                {
                    CodArticulo = "ART1",
                    GrupoEntry = 10,
                    Unidades = new List<UnidadArticuloDTO>
                    {
                        new() { UomEntry = 1, Codigo = "UND", Nombre = "Unidad", Factor = 1m, EsBase = true }
                    }
                }
            };
            _app.Setup(a => a.ObtenerAsync("ART1")).ReturnsAsync(buena);

            var r = await Crear().Obtener("ART1");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Obtener_ArticuloInexistente_DevuelveNotFound()
        {
            var mala = new Respuesta<UnidadesArticuloDTO> { Resultado = false, Mensaje = "El artículo X no existe." };
            _app.Setup(a => a.ObtenerAsync("X")).ReturnsAsync(mala);

            var r = await Crear().Obtener("X");

            Assert.Same(mala, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Obtener_OtroErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<UnidadesArticuloDTO>
            {
                Resultado = false,
                Mensaje = "El grupo de unidades de medida 99 del artículo ART1 no existe."
            };
            _app.Setup(a => a.ObtenerAsync("ART1")).ReturnsAsync(mala);

            var r = await Crear().Obtener("ART1");

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task UnidadArticuloApplication_MapeaUnidadesYCapturaExcepcion()
        {
            var svc = new Mock<IUnidadMedidaService>();
            svc.Setup(s => s.ObtenerUnidadesAsync("A1")).ReturnsAsync(new UnidadesArticulo("A1", 10, new[]
            {
                new UnidadArticulo(1, "UND", "Unidad", 1m, true),
                new UnidadArticulo(2, "CAJA", "Caja 12", 12m, false)
            }));
            svc.Setup(s => s.ObtenerUnidadesAsync("NO")).ThrowsAsync(new ArticuloNoExisteException("NO"));

            var app = new UnidadArticuloApplication(svc.Object);

            var ok = await app.ObtenerAsync("A1");
            Assert.True(ok.Resultado);
            Assert.Equal("A1", ok.Dato!.CodArticulo);
            Assert.Equal(2, ok.Dato.Unidades.Count);
            Assert.Equal(("CAJA", 12m, false), (ok.Dato.Unidades[1].Codigo, ok.Dato.Unidades[1].Factor, ok.Dato.Unidades[1].EsBase));

            var err = await app.ObtenerAsync("NO");
            Assert.False(err.Resultado);
            Assert.Equal("El artículo NO no existe.", err.Mensaje);
        }

        [Fact]
        public void PerfilMapeo_FacturaDetalle_MapeaUomCodigoEIgnoraFactorYCantidadBaseAlCrear()
        {
            var mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

            var entidad = new FacturaDetalle
            {
                Entry = 1,
                NoLinea = 1,
                CodArticulo = "A1",
                Cantidad = 3m,
                UomEntry = 2,
                FactorUom = 12m,
                CantidadBase = 36m,
                UomEntryNavigation = new UnidadMedidaArticulo { Entry = 2, Codigo = "CAJA" }
            };
            var dto = mapper.Map<FacturaDetalleDTO>(entidad);
            Assert.Equal(2, dto.UomEntry);
            Assert.Equal("CAJA", dto.UomCodigo);
            Assert.Equal(12m, dto.FactorUom);
            Assert.Equal(36m, dto.CantidadBase);

            var crear = new FacturaDetalleCrearDTO { CodArticulo = "A1", Cantidad = 3m, UomEntry = 2 };
            var desdeCrear = mapper.Map<FacturaDetalle>(crear);
            Assert.Equal(2, desdeCrear.UomEntry);
            Assert.Equal(1m, desdeCrear.FactorUom);
            Assert.Null(desdeCrear.CantidadBase);
        }
    }
}
