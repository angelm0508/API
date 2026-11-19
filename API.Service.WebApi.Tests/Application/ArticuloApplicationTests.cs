using API.Application.DTO.articulo.articulo;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    /// <summary>Al crear un artículo, un rechazo del dominio llega al cliente con el mensaje literal (como al actualizar).</summary>
    public class ArticuloApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        private ArticuloApplication Crear(Exception error)
        {
            var dominio = new Mock<IArticuloDomain>();
            dominio.Setup(d => d.InsertarAsync(It.IsAny<Articulo>())).ThrowsAsync(error);
            return new ArticuloApplication(dominio.Object, _mapper);
        }

        [Theory]
        [InlineData("Ya existe un artículo con el código de barras 7501234567890.")]
        [InlineData("El código de barras no puede tener más de 50 caracteres.")]
        public async Task Insertar_RechazoDelDominio_DevuelveElMensajeLiteral(string mensaje)
        {
            var respuesta = await Crear(new Exception(mensaje)).InsertarAsync(new ArticuloCrearDTO { Codigo = "A1", Serie = 3 });

            Assert.False(respuesta.Resultado);
            Assert.Equal(mensaje, respuesta.Mensaje);
        }

        [Fact]
        public async Task Insertar_ConExcepcionInterna_NoAgregaElTextoTecnico()
        {
            var error = new Exception("No se pudo guardar el artículo.", new InvalidOperationException("detalle tecnico de la base"));

            var respuesta = await Crear(error).InsertarAsync(new ArticuloCrearDTO { Codigo = "A1", Serie = 3 });

            Assert.False(respuesta.Resultado);
            Assert.Equal("No se pudo guardar el artículo.", respuesta.Mensaje);
        }
    }
}
