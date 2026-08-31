using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class ExistenciaApplication : IExistenciaApplication
    {
        private readonly IExistenciaDomain _domain;
        private readonly IMapper _mapper;

        public ExistenciaApplication(IExistenciaDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        public async Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerTodoAsync(string? articulo, string? almacen)
        {
            var respuesta = new Respuesta<IEnumerable<ExistenciaArticuloDTO>>();
            try
            {
                var lista = await _domain.ObtenerTodoAsync(articulo, almacen);
                respuesta.Dato = _mapper.Map<IEnumerable<ExistenciaArticuloDTO>>(lista);
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
                respuesta.Dato = e is not null
                    ? _mapper.Map<ExistenciaArticuloDTO>(e)
                    : new ExistenciaArticuloDTO { CodArticulo = codArticulo, CodAlmacen = codAlmacen, Disponible = 0m };
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
                respuesta.Dato = _mapper.Map<IEnumerable<ExistenciaArticuloDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }
    }
}
