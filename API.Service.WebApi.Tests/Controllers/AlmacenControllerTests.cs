using API.Application.DTO;
using API.Application.DTO.almacen;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class AlmacenControllerTests
    {
        private readonly Mock<IAlmacenApplication> _applicationMock;
        private readonly AlmacenController _controller;

        public AlmacenControllerTests()
        {
            _applicationMock = new Mock<IAlmacenApplication>();
            _controller = new AlmacenController(_applicationMock.Object);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorCodigo("A1");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorCodigo("A1");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorCodigo_DevuelveOk_CuandoExiste()
        {
            var dto = new AlmacenDTO { Codigo = "A1", Nombre = "Bodega", Activo = "S" };
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorCodigo("A1");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Bodega"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerPorNombre("Bodega");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveNotFound_CuandoDatoEsNulo()
        {
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Bodega"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.ObtenerPorNombre("Bodega");

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerPorNombre_DevuelveOk_CuandoExiste()
        {
            var dto = new AlmacenDTO { Codigo = "A1", Nombre = "Bodega", Activo = "S" };
            _applicationMock.Setup(a => a.ObtenerPorNombreAsync("Bodega"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = dto });

            var resultado = await _controller.ObtenerPorNombre("Bodega");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(dto, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Bod"))
                .ReturnsAsync(new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaNombre("Bod");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaNombre_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<AlmacenDTO> { new AlmacenDTO { Codigo = "A1", Activo = "S" } };
            _applicationMock.Setup(a => a.ObtenerContengaNombreAsync("Bod"))
                .ReturnsAsync(new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaNombre("Bod");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("A"))
                .ReturnsAsync(new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerContengaCodigo("A");

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerContengaCodigo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<AlmacenDTO> { new AlmacenDTO { Codigo = "A1", Activo = "S" } };
            _applicationMock.Setup(a => a.ObtenerContengaCodigoAsync("A"))
                .ReturnsAsync(new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerContengaCodigo("A");

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.ObtenerTodo();

            Assert.IsType<BadRequestObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var datos = new List<AlmacenDTO> { new AlmacenDTO { Codigo = "A1", Activo = "S" } };
            _applicationMock.Setup(a => a.ObtenerAsync())
                .ReturnsAsync(new Respuesta<IEnumerable<AlmacenDTO>> { Resultado = true, Dato = datos });

            var resultado = await _controller.ObtenerTodo();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(datos, ok.Value);
        }

        [Fact]
        public async Task Crear_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new AlmacenCrearDTO { Codigo = "A1", Activo = "S" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Crear_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new AlmacenCrearDTO { Codigo = "A1", Activo = "S" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Crear(crearDto);

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Actualizar("A1", new AlmacenActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<AlmacenActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Actualizar("A1", new AlmacenActualizarDTO());

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Actualizar_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            _applicationMock.Setup(a => a.ActualizarAsync("A1", It.IsAny<AlmacenActualizarDTO>()))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Actualizar("A1", new AlmacenActualizarDTO());

            Assert.IsType<OkResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveNotFound_CuandoNoExiste()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = null! });

            var resultado = await _controller.Eliminar("A1");

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            _applicationMock.Setup(a => a.EliminarAsync("A1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = false, Mensaje = "error" });

            var resultado = await _controller.Eliminar("A1");

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Eliminar_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerPorCodigoAsync("A1"))
                .ReturnsAsync(new Respuesta<AlmacenDTO> { Resultado = true, Dato = new AlmacenDTO { Codigo = "A1", Activo = "S" } });
            _applicationMock.Setup(a => a.EliminarAsync("A1"))
                .ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await _controller.Eliminar("A1");

            Assert.IsType<OkResult>(resultado);
        }
    }
}
