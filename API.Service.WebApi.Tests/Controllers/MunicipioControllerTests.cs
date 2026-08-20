using API.Application.DTO;
using API.Application.DTO.municipio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MunicipioControllerTests
    {
        private readonly Mock<IMunicipioApplication> _applicationMock;
        private readonly MunicipioController _controller;

        public MunicipioControllerTests()
        {
            _applicationMock = new Mock<IMunicipioApplication>();
            _controller = new MunicipioController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("MU1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("MU1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<MunicipioDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Código de municipio no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorCodigo("MU1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Municipio 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Municipio 1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Municipio 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Municipio 1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<MunicipioDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("Nombre de municipio no encontrado.", valor.Mensaje);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Municipio 1")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorNombre("Municipio 1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Muni")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Muni");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MunicipioDTO> { new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } };
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Muni")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaNombre("Muni");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("MU")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("MU");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MunicipioDTO> { new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } };
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("MU")).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerContengaCodigo("MU");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MunicipioDTO> { new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } };
            var respuesta = new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = true, Dato = datos };
            _applicationMock.Setup(a => a.ObtenerAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new MunicipioCrearDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new MunicipioCrearDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.Crear(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Actualizar("MU1", new MunicipioActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync("MU1", It.IsAny<MunicipioActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("MU1", new MunicipioActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync("MU1", It.IsAny<MunicipioActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.Actualizar("MU1", new MunicipioActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1")).ReturnsAsync(respuesta);

            var resultado = await _controller.Eliminar("MU1");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync("MU1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("MU1");

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync("MU1")).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.Eliminar("MU1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
