using API.Domain.Entity.Models;

namespace API.Application.Interface
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiraEn) GenerarToken(Usuario usuario);
    }
}
