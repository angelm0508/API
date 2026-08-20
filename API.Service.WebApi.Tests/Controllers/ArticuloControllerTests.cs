using API.Application.DTO;
using API.Application.DTO.articulo.articulo;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using API.Transversal.Common;
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
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Articulo("A1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.IsType<RespuestaError>(badRequest.Value);
        }

        [Fact]
        public async Task Articulo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Articulo("A1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Articulo_DevuelveOk_CuandoExiste()
        {
            var dto = new ArticuloDTO { Codigo = "A1", Nombre = "Producto" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.Articulo("A1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ArticuloPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Producto"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ArticuloPorNombre("Producto");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ArticuloPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Producto"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ArticuloPorNombre("Producto");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ArticuloPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new ArticuloDTO { Codigo = "A1", Nombre = "Producto" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Producto"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ArticuloPorNombre("Producto");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ArticulosContenganNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContenganNombreAsync("Prod"))
                .ReturnsAsync(new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ArticulosContenganNombre("Prod");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ArticulosContenganNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<ArticuloDTO> { new ArticuloDTO { Codigo = "A1" } };
            _applicationMock.Setup(a => a.ObtenerContenganNombreAsync("Prod"))
                .ReturnsAsync(new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ArticulosContenganNombre("Prod");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ArticulosContenganCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContenganCodigoAsync("A"))
                .ReturnsAsync(new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ArticulosContenganCodigo("A");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ArticulosContenganCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<ArticuloDTO> { new ArticuloDTO { Codigo = "A1" } };
            _applicationMock.Setup(a => a.ObtenerContenganCodigoAsync("A"))
                .ReturnsAsync(new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ArticulosContenganCodigo("A");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<ArticuloDTO> { new ArticuloDTO { Codigo = "A1" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.Obtener();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Post_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new ArticuloCrearDTO { Codigo = "A1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Post(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Post_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new ArticuloCrearDTO { Codigo = "A1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Post(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Update_DevuelveNotFound_CuandoNoExisteElArticulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Update("A1", new ArticuloActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Update_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<ArticuloActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Update("A1", new ArticuloActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Update_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<ArticuloActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Update("A1", new ArticuloActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Delete_DevuelveNotFound_CuandoNoExisteElArticulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Delete("A1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Delete_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            _applicationMock.Setup(a => a.EliminarAsync("A1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Delete("A1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Delete_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<ArticuloDTO> { Resultado = true, Dato = new ArticuloDTO { Codigo = "A1" } });
            _applicationMock.Setup(a => a.EliminarAsync("A1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Delete("A1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
