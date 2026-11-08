using API.Application.DTO;
using API.Application.DTO.articulo.unidades;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class UnidadArticuloApplication : IUnidadArticuloApplication
    {
        private readonly IUnidadMedidaService _unidades;

        public UnidadArticuloApplication(IUnidadMedidaService unidades)
        {
            _unidades = unidades;
        }

        public async Task<Respuesta<UnidadesArticuloDTO>> ObtenerAsync(string codigoArticulo)
        {
            var respuesta = new Respuesta<UnidadesArticuloDTO>();
            try
            {
                var r = await _unidades.ObtenerUnidadesAsync(codigoArticulo);
                respuesta.Dato = new UnidadesArticuloDTO
                {
                    CodArticulo = r.CodArticulo,
                    GrupoEntry = r.GrupoEntry,
                    Unidades = r.Unidades.Select(u => new UnidadArticuloDTO
                    {
                        UomEntry = u.UomEntry, Codigo = u.Codigo, Nombre = u.Nombre, Factor = u.Factor, EsBase = u.EsBase
                    }).ToList()
                };
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Resultado = false;
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
    }
}
