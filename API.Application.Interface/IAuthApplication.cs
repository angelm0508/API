using API.Application.DTO;
using API.Application.DTO.autenticacion;

namespace API.Application.Interface
{
    public interface IAuthApplication
    {
        Task<Respuesta<LoginResponseDTO>> LoginAsync(LoginDTO obj);
    }
}
