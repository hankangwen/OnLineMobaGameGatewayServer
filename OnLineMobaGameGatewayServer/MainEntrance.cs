using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnLineMobaGameServer
{
    public class MainEntrance
    {
        public static void Main(string[] args)
        {
            Gateway.Connect("127.0.0.1", 8888);
        }
    }
}
