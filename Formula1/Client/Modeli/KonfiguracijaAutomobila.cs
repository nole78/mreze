using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Enumeracije;



namespace Client.Modeli
{
    public class KonfiguracijaAutomobila
    {
        public Timovi Tim { get; set; } = 0;
        public TipGume TipGume { get; set; } = 0;
        public double PotrosnjaGuma { get; set; } = 0;
        public double PotrosnjaGoriva { get; set; } = 0;
        public int StanjeGoriva { get; set; } = 0;
        public int StanjeGuma { get; set; } = 0;

        public KonfiguracijaAutomobila() { }
        public KonfiguracijaAutomobila(Timovi tim, double potrosnjaGuma, double potrosnjaGoriva, TipGume tipGume)
        {
            Tim = tim;
            PotrosnjaGuma = potrosnjaGuma;
            PotrosnjaGoriva = potrosnjaGoriva;
            TipGume = tipGume;
        }
    }
}

