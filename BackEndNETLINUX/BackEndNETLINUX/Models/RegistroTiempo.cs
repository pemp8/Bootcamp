namespace BackEndNETLINUX.Models
{
    public class RegistroTiempo
    {
        public int ID { get; set; }
        public int TrabajadorID { get; set; }
        public Trabajador Trabajador { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan TiempoEntrada { get; set; }
        public TimeSpan TiempoSalida { get; set; }
        public TimeSpan? TiempoTrabajado { get; set; }

        public void CalcularTiempoTrabajado()
        {
            TiempoTrabajado = TiempoSalida - TiempoEntrada;
        }
    }
}
