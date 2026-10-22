namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioEstadoDTO
    {
        /// <summary>Nuevo estado: P (En proceso), T (Terminada) o E (Entregada).</summary>
        public string Estado { get; set; } = null!;
    }
}
