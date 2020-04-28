using AdministradorDePagos.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdministradorDePagos.Core
{
    public class FMensaje
    {

        public List<Mensaje> Lista = new List<Mensaje>();

        public void Limpiar()
        {
            this.Lista.Clear();
        }

        public void Agregar(Mensaje mensaje)
        {
            this.Lista.Add(new Mensaje() { Texto = mensaje.Texto, Tipo = mensaje.Tipo });
        }

        public void Agregar(string texto, ETipoMensaje tipo = ETipoMensaje.Error)
        {
            this.Lista.Add(new Mensaje() { Texto = texto, Tipo = tipo });
        }

        public void Agregar(List<Mensaje> mensajes)
        {
            mensajes.ForEach(m => Agregar(m));
        }

    }
}
