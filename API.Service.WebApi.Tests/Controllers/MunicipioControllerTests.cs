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
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("MU1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("MU1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("MU1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Municipio 1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorNombre("Municipio 1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Municipio 1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorNombre("Municipio 1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Municipio 1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorNombre("Municipio 1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Muni"))
                .ReturnsAsync(new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaNombre("Muni");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MunicipioDTO> { new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Muni"))
                .ReturnsAsync(new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaNombre("Muni");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("MU"))
                .ReturnsAsync(new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaCodigo("MU");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MunicipioDTO> { new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("MU"))
                .ReturnsAsync(new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaCodigo("MU");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodo();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<MunicipioDTO> { new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<MunicipioDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new MunicipioCrearDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new MunicipioCrearDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Actualizar("MU1", new MunicipioActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("MU1", It.IsAny<MunicipioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Actualizar("MU1", new MunicipioActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            _applicationMock.Setup(a => a.ActualizarAsync("MU1", It.IsAny<MunicipioActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Actualizar("MU1", new MunicipioActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Eliminar("MU1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            _applicationMock.Setup(a => a.EliminarAsync("MU1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Eliminar("MU1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("MU1"))
                .ReturnsAsync(new Respuesta<MunicipioDTO> { Resultado = true, Dato = new MunicipioDTO { Codigo = "MU1", Departamento = "D1", Pais = "P1", Nombre = "Municipio 1" } });
            _applicationMock.Setup(a => a.EliminarAsync("MU1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Eliminar("MU1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
