using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Core;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class ExistenciaApplication : IExistenciaApplication
    {
        private readonly IExistenciaDomain _domain;
        private readonly IMapper _mapper;
        private readonly IArticuloAlmacenDomain _configuracion;

        public ExistenciaApplication(IExistenciaDomain domain, IMapper mapper, IArticuloAlmacenDomain configuracion)
        {
            _domain = domain;
            _mapper = mapper;
            _configuracion = configuracion;
        }

        public async Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerTodoAsync(string? articulo, string? almacen)
        {
            var respuesta = new Respuesta<IEnumerable<ExistenciaArticuloDTO>>();
            try
            {
                var lista = await _domain.ObtenerTodoAsync(articulo, almacen);
                var dtos = _mapper.Map<IEnumerable<ExistenciaArticuloDTO>>(lista).ToList();
                await EnriquecerAsync(dtos, articulo, almacen);
                respuesta.Dato = dtos;
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }

        public async Task<Respuesta<ExistenciaArticuloDTO>> ObtenerAsync(string codArticulo, string codAlmacen)
        {
            var respuesta = new Respuesta<ExistenciaArticuloDTO>();
            try
            {
                var e = await _domain.ObtenerAsync(codArticulo, codAlmacen);
                var dto = e is not null
                    ? _mapper.Map<ExistenciaArticuloDTO>(e)
                    : new ExistenciaArticuloDTO { CodArticulo = codArticulo, CodAlmacen = codAlmacen, Disponible = 0m };
                await EnriquecerAsync(new[] { dto }, codArticulo, codAlmacen);
                respuesta.Dato = dto;
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerPorArticuloAsync(string codArticulo)
        {
            var respuesta = new Respuesta<IEnumerable<ExistenciaArticuloDTO>>();
            try
            {
                var lista = await _domain.ObtenerPorArticuloAsync(codArticulo);
                var dtos = _mapper.Map<IEnumerable<ExistenciaArticuloDTO>>(lista).ToList();
                await EnriquecerAsync(dtos, codArticulo, null);
                respuesta.Dato = dtos;
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }

        /// <summary>
        /// Llena Minimo, Maximo, Bloqueado y Estado con UNA consulta de configuración por petición.
        /// Sin configuración: nulos, sin bloqueo y estado OK.
        /// </summary>
        private async Task EnriquecerAsync(IReadOnlyCollection<ExistenciaArticuloDTO> dtos, string? articulo, string? almacen)
        {
            var config = await _configuracion.ObtenerConfiguracionAsync(articulo, almacen);
            foreach (var dto in dtos)
            {
                if (config.TryGetValue((dto.CodArticulo, dto.CodAlmacen), out var c))
                {
                    dto.Minimo = c.Minimo;
                    dto.Maximo = c.Maximo;
                    dto.Bloqueado = c.Bloqueado;
                }
                dto.Estado = StockAlmacenCalculadora.Estado(dto.Disponible, dto.Minimo, dto.Maximo);
            }
        }
    }
}
