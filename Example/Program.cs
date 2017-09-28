using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Example
{
    class Program
    {
        static void Main(string[] args)
        {
            PingPong pp = new PingPong();

            pp.Run();

            while (true)
            {
                Task.Delay(10);
            }
        }
    }
}
