using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NETLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            for ( int i = 0; i < 1000; ++i)
            {
                Logger.Instance.debug("Test " + i);
            }

            try
            {
                FileStream fs = new FileStream(@"C:\temp.txt", FileMode.Open);
            }
            catch (Exception e)
            {
                Logger.Instance.exception("Thrown from main.", e);
            }
        }
    }
}