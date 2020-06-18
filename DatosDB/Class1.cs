using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DatosDB
{
    public class Class1
    {

        static void Main(string[] args)
        {
            LeerStatic();
        }

        static void LeerStatic()
        {
            //using (masterEntities db = new masterEntities())
            //{
            //    var lst = db.PagoEfectuado;
            //
            //    foreach (var item in lst)
            //    {
            //        Console.WriteLine(item.Concepto);
            //    }
            //
            //
            //}
        }

        public string Leer()
        {
            //masterEntities obj = new masterEntities();
            //obj.PagoEfectuado = null;

            string retorno = "";

            try
            {
                using (masterEntities1 db = new masterEntities1())
                {
                    var lst = db.PagoEfectuado;

                    foreach (var item in lst)
                    {
                        retorno += item.Path;

                        //Console.WriteLine(item.Concepto);
                    }


                }
            }
            catch (Exception ex)
            {

            }

            //using (masterEntities db = new masterEntities())
            //{
            //    var lst = db.PagoEfectuado;
            //
            //    //obj.PagoEfectuado = lst;
            //
            //    foreach (var item in lst)
            //    {
            //        retorno += item.Path;
            //
            //        //Console.WriteLine(item.Concepto);
            //    }
            //
            //
            //}

            return retorno;
        }

    }
}