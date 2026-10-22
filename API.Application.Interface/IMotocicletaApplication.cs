using API.Application.DTO;
using API.Application.DTO.motocicleta;
using API.Application.DTO.ordenServicio;

namespace API.Application.Interface
{
    public interface IMotocicletaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(MotocicletaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, MotocicletaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry);
        Task<Respuesta<MotocicletaDTO>> ObtenerAsync(int entry);
        Task<Respuesta<IEnumerable<MotocicletaDTO>>> BuscarAsync(string? codigoSn, string? placa);
        Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerHistorialAsync(int entry);
        #endregion
    }
}
