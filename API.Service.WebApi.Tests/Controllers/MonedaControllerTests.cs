using API.Application.DTO;
using API.Application.DTO.moneda;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MonedaControllerTests
    {
        private readonly Mock<IMonedaApplication> _applicationMock;
        private readonly MonedaController _controller;

        public MonedaControllerTests()
        {
            _applicationMock = new Mock<IMonedaApplication>();
            _controller = new MonedaController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("M1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("M1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new MonedaDTO { Codigo = "M1", Nombre = "Quetzal" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("M1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Quetzal"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorNombre("Quetzal");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Quetzal"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorNombre("Quetzal");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new MonedaDTO { Codigo = "M1", Nombre = "Quetzal" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Quetzal"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorNombre("Quetzal");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Que"))
                .ReturnsAsync(new Respuesta<IEnumerable<MonedaDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaNombre("Que");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MonedaDTO> { new MonedaDTO { Codigo = "M1" } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Que"))
                .ReturnsAsync(new Respuesta<IEnumerable<MonedaDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaNombre("Que");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("M"))
                .ReturnsAsync(new Respuesta<IEnumerable<MonedaDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaCodigo("M");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MonedaDTO> { new MonedaDTO { Codigo = "M1" } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("M"))
                .ReturnsAsync(new Respuesta<IEnumerable<MonedaDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaCodigo("M");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<MonedaDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodo();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MonedaDTO> { new MonedaDTO { Codigo = "M1" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<MonedaDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new MonedaCrearDTO { Codigo = "M1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new MonedaCrearDTO { Codigo = "M1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Actualizar("M1", new MonedaActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("M1", It.IsAny<MonedaActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Actualizar("M1", new MonedaActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("M1", It.IsAny<MonedaActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Actualizar("M1", new MonedaActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Eliminar("M1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            _applicationMock.Setup(a => a.EliminarAsync("M1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Eliminar("M1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("M1"))
                .ReturnsAsync(new Respuesta<MonedaDTO> { Resultado = true, Dato = new MonedaDTO { Codigo = "M1" } });
            _applicationMock.Setup(a => a.EliminarAsync("M1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Eliminar("M1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
