using Client.Enumeracije;
using Client.Modeli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.PomocneFunkcije
{
    public static class RacunanjePotrosnje
    {
        public static (double,double) RacunajPotrosnju(Timovi tim) // vraca potrosnju guma i goriva
        {
            if (tim == Timovi.Mercedes)
            {
                return (0.3, 0.6);
            }
            else if (tim == Timovi.Ferari)
            {
                return (0.3, 0.5);
            }
            else if (tim == Timovi.Reno)
            {
                return (0.4, 0.7);
            }
            else if (tim == Timovi.Honda)
            {
                return (0.2, 0.6);
            }
            else
                return (0, 0);
        }
    }
}
