namespace API.Domain.Interface
{
    public interface IPermisoDomain
    {
        Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizUsuarioAsync(int usuarioId);
        Task GuardarMatrizUsuarioAsync(int usuarioId, IReadOnlyList<PermisoModulo> matriz);
        Task<IReadOnlyList<PermisoModulo>> ObtenerMatrizPlantillaAsync(int plantillaId);
        Task GuardarMatrizPlantillaAsync(int plantillaId, IReadOnlyList<PermisoModulo> matriz);
        Task AplicarPlantillaAsync(int usuarioId, int plantillaId);
    }
}
