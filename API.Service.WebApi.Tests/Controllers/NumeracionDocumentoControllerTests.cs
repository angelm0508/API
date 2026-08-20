using API.Application.DTO;
using API.Application.DTO.numeracionDocumento;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class NumeracionDocumentoControllerTests
    {
        private readonly Mock<INumeracionDocumentoApplication> _applicationMock;
        private readonly NumeracionDocumentoController _controller;

        public NumeracionDocumentoControllerTests()
        {
            _applicationMock = new Mock<INumeracionDocumentoApplication>();
            _controller = new NumeracionDocumentoController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("ND1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("ND1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("ND1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("N"))
                .ReturnsAsync(new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaCodigo("N");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<NumeracionDocumentoDTO> { new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("N"))
                .ReturnsAsync(new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaCodigo("N");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodo();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<NumeracionDocumentoDTO> { new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<NumeracionDocumentoDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new NumeracionDocumentoCrearDTO { CodigoObj = "ND1", SubTipoDoc = "F" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new NumeracionDocumentoCrearDTO { CodigoObj = "ND1", SubTipoDoc = "F" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Actualizar("ND1", new NumeracionDocumentoActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Actualizar("ND1", new NumeracionDocumentoActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.ActualizarAsync("ND1", It.IsAny<NumeracionDocumentoActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Actualizar("ND1", new NumeracionDocumentoActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Eliminar("ND1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.EliminarAsync("ND1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Eliminar("ND1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("ND1"))
                .ReturnsAsync(new Respuesta<NumeracionDocumentoDTO> { Resultado = true, Dato = new NumeracionDocumentoDTO { CodigoObj = "ND1", SubTipoDoc = "F" } });
            _applicationMock.Setup(a => a.EliminarAsync("ND1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Eliminar("ND1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
