using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace Common
{
    public enum Timovi { Mercedes, Ferari, Reno, Honda };
    public enum TipGume { Meke, SrednjeTvrde, Tvrde }
    public class KonfiguracijaAutomobila
    {
        public Timovi Tim { get; set; }
        public TipGume TipGume { get; set; }
        public double PotrosnjaGuma { get; set; }
        public double PotrosnjaGoriva { get; set; }
        public int StanjeGoriva { get; set; }
        public int StanjeGuma { get; set; }

        public KonfiguracijaAutomobila() { }
        public KonfiguracijaAutomobila(Timovi tim, double potrosnjaGuma, double potrosnjaGoriva, TipGume tipGume)
        {
            this.Tim = tim;
            this.PotrosnjaGuma = potrosnjaGuma;
            this.PotrosnjaGoriva = potrosnjaGoriva;
            this.TipGume = tipGume;
        }
    }
}

