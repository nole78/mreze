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
        public Timovi Tim { get; set; } = 0; // Tim: NEMA_TIM, Mercedes, Ferari, Reno, Honda
        public TipGume TipGume { get; set; } = 0; // Tip gume: Meke, SrednjeTvrde, Tvrde
        public double PotrosnjaGuma { get; set; } = 0; // Potrošnja guma
        public double PotrosnjaGoriva { get; set; } = 0; // Potrošnja goriva
        public double StanjeGoriva { get; set; } = 0; // Kolicina goriva u litrima
        public double StanjeGuma { get; set; } = 0; // Trajanje guma u kilometrima

        public KonfiguracijaAutomobila() { }
        public KonfiguracijaAutomobila(Timovi tim, double potrosnjaGuma, double potrosnjaGoriva, TipGume tipGume)
        {
            Tim = tim;
            PotrosnjaGuma = potrosnjaGuma;
            PotrosnjaGoriva = potrosnjaGoriva;
            TipGume = tipGume;
        }
    
        public double GetTrajanjeGuma()
        {
            switch (TipGume)
            {
                case TipGume.Meke:
                    return 80;
                case TipGume.SrednjeTvrde:
                    return 100;
                case TipGume.Tvrde:
                    return 120;
                default:
                    return 0;
            }
        }
    }
}

