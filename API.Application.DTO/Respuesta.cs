namespace API.Application.DTO
{
    public class Respuesta<T>
    {
        public T Dato { get; set; }
        public bool Resultado { get; set; }
        public string Mensaje { get; set; }
    }
}
