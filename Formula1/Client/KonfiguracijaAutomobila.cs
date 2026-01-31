using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum Timovi { Mercedes, Ferari, Reno, Honda };
public enum TipGume { Meke, SrednjeTvrde, Tvrde }

namespace Client
{
    internal class KonfiguracijaAutomobila
    {
        Timovi tim;
        TipGume tipGume;
        double potrosnjaGuma;
        double potrosnjaGoriva;
        int stanjeGoriva;
        int stanjeGuma;

        KonfiguracijaAutomobila(Timovi tim, double potrosnjaGuma, double potrosnjaGoriva, TipGume tipGume)
        {
            this.tim = tim;
            this.potrosnjaGuma = potrosnjaGuma;
            this.potrosnjaGoriva = potrosnjaGoriva;
            this.tipGume = tipGume;
        }
    }
}

