namespace API.Transversal.Common
{
    public class RespuestaError
    {
        public RespuestaError(string mensaje)
        {
            Mensaje = new string[] { mensaje };
        }
        public string[] Mensaje { get; set; }
    }
}
