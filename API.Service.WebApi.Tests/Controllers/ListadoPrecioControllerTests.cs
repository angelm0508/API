using API.Application.DTO;
using API.Application.DTO.precio.listado_precio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ListadoPrecioControllerTests
    {
        private readonly Mock<IListadoPrecioApplication> _applicationMock;
        private readonly ListadoPrecioController _controller;

        public ListadoPrecioControllerTests()
        {
            _applicationMock = new Mock<IListadoPrecioApplication>();
            _controller = new ListadoPrecioController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Obtener(1);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new ListadoPrecioDTO { Codigo = 1, Nombre = "Listado 1" };
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("Listado 1"))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorNombre("Listado 1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerAsync("Listado 1"))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorNombre("Listado 1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new ListadoPrecioDTO { Codigo = 1, Nombre = "Listado 1" };
            _applicationMock.Setup(a => a.ObtenerAsync("Listado 1"))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorNombre("Listado 1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Lis"))
                .ReturnsAsync(new Respuesta<IEnumerable<ListadoPrecioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObteneContengaNombreAsync("Lis");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObteneContengaNombreAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<ListadoPrecioDTO> { new ListadoPrecioDTO { Codigo = 1 } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Lis"))
                .ReturnsAsync(new Respuesta<IEnumerable<ListadoPrecioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObteneContengaNombreAsync("Lis");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ListadoPrecioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodoAsync();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<ListadoPrecioDTO> { new ListadoPrecioDTO { Codigo = 1 } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<ListadoPrecioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new ListadoPrecioCrearDTO { Nombre = "Listado 1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<int> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new ListadoPrecioCrearDTO { Nombre = "Listado 1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<int> { Resultado = true, Dato = 1 });

            var resultado = await _controller.InsertarAsync(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ActualizarAsync(1, new ListadoPrecioActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = new ListadoPrecioDTO { Codigo = 1 } });
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<ListadoPrecioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ActualizarAsync(1, new ListadoPrecioActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = new ListadoPrecioDTO { Codigo = 1 } });
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<ListadoPrecioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.ActualizarAsync(1, new ListadoPrecioActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = new ListadoPrecioDTO { Codigo = 1 } });
            _applicationMock.Setup(a => a.EliminarAsync(1))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<ListadoPrecioDTO> { Resultado = true, Dato = new ListadoPrecioDTO { Codigo = 1 } });
            _applicationMock.Setup(a => a.EliminarAsync(1))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.EliminarAsync(1);

            Assert.IsType<OkResult>(resultado);
        }
    }
}
