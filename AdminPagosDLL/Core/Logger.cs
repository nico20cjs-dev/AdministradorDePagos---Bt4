using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdminPagosDLL.Core
{
    public static class Logger
    {
        public static void Add(string msj)
        {
            string strExeFilePath = AppDomain.CurrentDomain.BaseDirectory;            
            string strWorkPath = System.IO.Path.GetDirectoryName(strExeFilePath);
            var file = System.IO.Path.Combine(strExeFilePath, "log.txt");
            ;
            if (!File.Exists(file))
                File.Create(file);

            using (StreamWriter sw = new StreamWriter(file, true))
            {
                sw.WriteLine(msj);
            }
        }
    }
}
