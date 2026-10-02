namespace API.Application.DTO.permiso
{
    public class PermisoModuloDTO
    {
        public string CodigoModulo { get; set; } = null!;
        public bool Ver { get; set; }
        public bool Agregar { get; set; }
        public bool Actualizar { get; set; }
        public bool Eliminar { get; set; }
    }
}
