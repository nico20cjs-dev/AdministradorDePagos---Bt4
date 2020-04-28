using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdministradorDePagos.Core
{
    public class Formatos
    {
		public FMensaje Mensajes = new FMensaje();

		public DateTime CrearFecha(string fecha)
        {
			DateTime retorno = new DateTime();
			try
			{
				var auxFecha = fecha.Trim().Split(' ');

				var aux = auxFecha[0].Split('-');
				int dia = int.Parse(aux[0]);
				int mes = int.Parse(aux[1]);
				int año = int.Parse(aux[2]);

				int hora = 0;
				int min = 0;
				int seg = 0;

				//Si tiene HH:mm:ss
				if (auxFecha.Count() > 1)
				{
					var auxHora = auxFecha[1].Split('-');
					hora = int.Parse(auxHora[0]);
					min = int.Parse(auxHora[1]);
					seg = int.Parse(auxHora[2]);
				}

				retorno = new DateTime(año, mes, dia, hora, min, seg);

				return retorno;
			}
			catch (Exception ex)
			{
				return retorno;
			}
        }

    }
}
