using API.Application.DTO;
using API.Application.DTO.pais;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PaisControllerTests
    {
        private readonly Mock<IPaisApplication> _applicationMock;
        private readonly PaisController _controller;

        public PaisControllerTests()
        {
            _applicationMock = new Mock<IPaisApplication>();
            _controller = new PaisController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("GT");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("GT");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new PaisDTO { Codigo = "GT", Nombre = "Guatemala" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("GT");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Guatemala"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorNombre("Guatemala");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Guatemala"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorNombre("Guatemala");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new PaisDTO { Codigo = "GT", Nombre = "Guatemala" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Guatemala"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorNombre("Guatemala");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Guat"))
                .ReturnsAsync(new Respuesta<IEnumerable<PaisDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaNombre("Guat");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<PaisDTO> { new PaisDTO { Codigo = "GT" } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Guat"))
                .ReturnsAsync(new Respuesta<IEnumerable<PaisDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaNombre("Guat");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("G"))
                .ReturnsAsync(new Respuesta<IEnumerable<PaisDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaCodigo("G");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<PaisDTO> { new PaisDTO { Codigo = "GT" } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("G"))
                .ReturnsAsync(new Respuesta<IEnumerable<PaisDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaCodigo("G");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<PaisDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodo();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<PaisDTO> { new PaisDTO { Codigo = "GT" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<PaisDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new PaisCrearDTO { Codigo = "GT" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new PaisCrearDTO { Codigo = "GT" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Actualizar("GT", new PaisActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            _applicationMock.Setup(a => a.ActualizarAsync("GT", It.IsAny<PaisActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Actualizar("GT", new PaisActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            _applicationMock.Setup(a => a.ActualizarAsync("GT", It.IsAny<PaisActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Actualizar("GT", new PaisActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Eliminar("GT");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            _applicationMock.Setup(a => a.EliminarAsync("GT"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Eliminar("GT");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("GT"))
                .ReturnsAsync(new Respuesta<PaisDTO> { Resultado = true, Dato = new PaisDTO { Codigo = "GT" } });
            _applicationMock.Setup(a => a.EliminarAsync("GT"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Eliminar("GT");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
