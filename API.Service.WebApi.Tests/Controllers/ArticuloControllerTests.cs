using API.Application.DTO;
using API.Application.DTO.articulo.articulo;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ArticuloControllerTests
    {
        private readonly Mock<IArticuloApplication> _applicationMock;
        private readonly ArticuloController _controller;

        public ArticuloControllerTests()
        {
            _applicationMock = new Mock<IArticuloApplication>();
            _controller = new ArticuloController(_applicationMock.Object);
        }

        [Fact]
        public async Task Articulo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Articulo("A1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Articulo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Articulo("A1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<ArticuloDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de articulo no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task Articulo_DevuelveOk_CuandoExiste()
        {
            var dto = new ArticuloDTO { Codigo = "A1", Nombre = "Producto" };
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Articulo("A1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ArticuloPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Producto")).ReturnsAsync(respuesta);

            var resultado = await _controller.ArticuloPorNombre("Producto");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ArticuloPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Producto")).ReturnsAsync(respuesta);

            var resultado = await _controller.ArticuloPorNombre("Producto");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<ArticuloDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Nombre de articulo no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ArticuloPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new ArticuloDTO { Codigo = "A1", Nombre = "Producto" };
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Producto")).ReturnsAsync(respuesta);

            var resultado = await _controller.ArticuloPorNombre("Producto");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ArticulosContenganNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContenganNombreAsync("Prod")).ReturnsAsync(respuesta);

            var resultado = await _controller.ArticulosContenganNombre("Prod");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ArticulosContenganNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = true, Dato = new List<ArticuloDTO> { new ArticuloDTO { Codigo = "A1" } } };
            _applicationMock.Setup(a => a.ObtenerContenganNombreAsync("Prod")).ReturnsAsync(respuesta);

            var resultado = await _controller.ArticulosContenganNombre("Prod");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ArticulosContenganCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContenganCodigoAsync("A")).ReturnsAsync(respuesta);

            var resultado = await _controller.ArticulosContenganCodigo("A");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ArticulosContenganCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = true, Dato = new List<ArticuloDTO> { new ArticuloDTO { Codigo = "A1" } } };
            _applicationMock.Setup(a => a.ObtenerContenganCodigoAsync("A")).ReturnsAsync(respuesta);

            var resultado = await _controller.ArticulosContenganCodigo("A");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = true, Dato = new List<ArticuloDTO> { new ArticuloDTO { Codigo = "A1" } } };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Post_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new ArticuloCrearDTO { Codigo = "A1" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Post(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Post_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new ArticuloCrearDTO { Codigo = "A1" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Post(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Update_DevuelveNotFound_CuandoNoExisteElArticulo()
        {
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Update("A1", new ArticuloActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Update_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<ArticuloActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Update("A1", new ArticuloActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Update_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<ArticuloActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Update("A1", new ArticuloActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Delete_DevuelveNotFound_CuandoNoExisteElArticulo()
        {
            var respuesta = new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Delete("A1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Delete_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("A1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Delete("A1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Delete_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("A1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Delete("A1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
