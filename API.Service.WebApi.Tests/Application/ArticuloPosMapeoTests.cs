using API.Application.DTO.articulo.articulo;
using API.Domain.Entity.Models;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>Codigo de barras y marca "mostrar en POS" del articulo: qué copia AutoMapper en los tres mapeos.</summary>
    public class ArticuloPosMapeoTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        [Fact]
        public void CrearDto_PropagaCodigoBarrasYMostrarEnPos()
        {
            var entidad = _mapper.Map<Articulo>(new ArticuloCrearDTO { Codigo = "A1", Serie = 1, CodigoBarras = "7501234567890", MostrarEnPos = "S" });

            Assert.Equal("7501234567890", entidad.CodigoBarras);
            Assert.Equal("S", entidad.MostrarEnPos);
        }

        [Fact]
        public void CrearDto_SinValoresUsaDefectos()
        {
            var dto = new ArticuloCrearDTO { Codigo = "A1", Serie = 1 };
            var entidad = _mapper.Map<Articulo>(dto);

            Assert.Equal("N", dto.MostrarEnPos);
            Assert.Null(entidad.CodigoBarras);
            Assert.Equal("N", entidad.MostrarEnPos);
        }

        [Fact]
        public void ActualizarDto_PropagaCodigoBarrasYMostrarEnPos()
        {
            var entidad = _mapper.Map<Articulo>(new ArticuloActualizarDTO { Codigo = "A1", Serie = 1, CodigoBarras = "123", MostrarEnPos = "S" });

            Assert.Equal("123", entidad.CodigoBarras);
            Assert.Equal("S", entidad.MostrarEnPos);
        }

        [Fact]
        public void ActualizarDto_SinCodigoBarrasLoBorra_ElPutReemplazaColumnas()
        {
            // El PUT reemplaza todas las columnas: la Web debe enviar siempre los valores actuales.
            var existente = new Articulo { Codigo = "A1", Serie = 1, CodigoBarras = "123", MostrarEnPos = "S" };

            _mapper.Map(new ArticuloActualizarDTO { Codigo = "A1", Serie = 1, MostrarEnPos = "S" }, existente);

            Assert.Null(existente.CodigoBarras);
            Assert.Equal("S", existente.MostrarEnPos);
        }

        [Fact]
        public void EntidadAConsultaDto_PropagaCodigoBarrasYMostrarEnPos()
        {
            var dto = _mapper.Map<ArticuloDTO>(new Articulo { Codigo = "A1", Serie = 1, CodigoBarras = "7501234567890", MostrarEnPos = "S" });

            Assert.Equal("7501234567890", dto.CodigoBarras);
            Assert.Equal("S", dto.MostrarEnPos);
        }
    }
}
