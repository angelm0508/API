using API.Domain.Entity.Models;

namespace API.Application.Interface
{
    public interface ITokenService
    {
        Task<(string Token, DateTime ExpiraEn)> GenerarTokenAsync(Usuario usuario);
    }
}
