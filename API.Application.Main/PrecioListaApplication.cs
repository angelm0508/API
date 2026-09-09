using API.Application.DTO;
using API.Application.DTO.precio;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class PrecioListaApplication : IPrecioListaApplication
    {
        private readonly IPrecioListaDomain _domain;

        public PrecioListaApplication(IPrecioListaDomain domain)
        {
            _domain = domain;
        }

        public async Task<Respuesta<PrecioResueltoDTO>> ResolverAsync(string codigoArticulo, int? listaPrecioEntry)
        {
            var respuesta = new Respuesta<PrecioResueltoDTO>();
            try
            {
                var r = await _domain.ResolverAsync(codigoArticulo, listaPrecioEntry);
                respuesta.Dato = new PrecioResueltoDTO
                {
                    Codigo = r.Codigo,
                    PrecioBase = r.PrecioBase,
                    FactorEfectivo = r.FactorEfectivo,
                    PrecioLista = r.PrecioLista
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
