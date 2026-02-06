using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;
using Client.Enumeracije;
using Client.Modeli;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket? сокет;
        private Socket? UdpSoket;
        private CancellationTokenSource? _cts;
        private Task? _rxTask;
        private KonfiguracijaAutomobila bolid = new KonfiguracijaAutomobila();
        private string? trkacki_broj = "";
        private NacinVoznje nacinVoznje = NacinVoznje.Normalno;
        double osnovno_vreme = 0;


        public int port = 0;
        public MainWindow()
        {
            InitializeComponent();
            OtvoriOdabirTima();
        }

        private void TcpKonekcija(int port)
        {
            сокет = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, port);
            try
            {
                сокет.Connect(serverEP);

                _cts = new CancellationTokenSource();
                _rxTask = Task.Run(() => ReceiveLoopTcp(_cts.Token));
            }
            catch (Exception ex)
            {
                // Ispiši grešku umesto MessageBox
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Ne mogu da se povežem: {ex.Message}\n");
                    chatBox.ScrollToEnd();
                });

                // Ponovo otvori prozor za izbor tima

                Dispatcher.Invoke(() =>
                {
                    OtvoriOdabirTima();
                });            
            }
        }

        private void ReceiveLoopTcp(CancellationToken token)
        {
            byte[] buf = new byte[4096];

            while (!token.IsCancellationRequested)
            {
                Socket? s = сокет;
                if (s == null) break;

                try
                {
                    int n = s.Receive(buf);
                    if (n == 0)
                    {
                        Dispatcher.Invoke(() => Disconnect());
                        break;
                    }

                    string text = Encoding.UTF8.GetString(buf, 0, n);
                    ObradiPoruku(text);
                }
                catch (SocketException ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[GREŠKA - SocketException] {ex.Message}\n");
                        chatBox.ScrollToEnd();
                        Disconnect();
                    });
                    break;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[GREŠKA] {ex.Message}\n");
                        chatBox.ScrollToEnd();
                        Disconnect();
                    });
                    break;
                }
            }
        }

        private void OtvoriUdpKonekciju()
        {
            UdpSoket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint destinationEP = new IPEndPoint(IPAddress.Loopback, 50005);

            Dispatcher.Invoke(() =>
            {
                chatBox.AppendText($"Otvorena UDP utičnica");
                chatBox.ScrollToEnd();
            });

        }

        private void ObradiPoruku(string poruka)
        {
            // Ako server odbije konekciju, otvori ponovo izbor tima
            int broj;

            if (poruka.Contains("Nema više mesta u timu"))
            {
                Dispatcher.Invoke(() =>
                {
                    OtvoriOdabirTima();
                });
            }
            else if (int.TryParse(poruka, out broj) && broj >= 1 && broj <= 100)
            {
                trkacki_broj = broj.ToString();
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Dodeljen trkački broj: {trkacki_broj}\n");
                    chatBox.ScrollToEnd();
                });
            }
            else if(bolid.Tim == 0)
            {
                OtvoriUdpKonekciju();
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText(poruka + "\n");
                    chatBox.ScrollToEnd();
                });
            }
        }

        private void Disconnect()
        {
            try
            {
                if (_cts != null) _cts.Cancel();

                if (сокет != null)
                {
                    try { сокет.Shutdown(SocketShutdown.Both); } catch { }
                    try { сокет.Close(); } catch { }
                }
                сокет = null;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA pri gašenju] {ex.Message}\n");
                    chatBox.ScrollToEnd();
                });
            }
        }

        public void RacunajPotrosnju(Timovi tim)
        {
            if (tim == Timovi.Mercedes)
            {
                bolid.PotrosnjaGuma = 0.3;
                bolid.PotrosnjaGoriva = 0.6;
            }
            else if (tim == Timovi.Ferari)
            {
                bolid.PotrosnjaGuma = 0.3;
                bolid.PotrosnjaGoriva = 0.5;
            }
            else if (tim == Timovi.Reno)
            {
                bolid.PotrosnjaGuma = 0.4;
                bolid.PotrosnjaGoriva = 0.7;
            }
            else if (tim == Timovi.Honda)
            {
                bolid.PotrosnjaGuma = 0.2;
                bolid.PotrosnjaGoriva = 0.6;
            }
        }

        private void OtvoriOdabirTima()
        {
            OdabirTima odabirTima = new OdabirTima();
            
            if (odabirTima.ShowDialog() == true)
            {
                Timovi tim = odabirTima.izabraniTim;
                bolid.Tim = tim;
                RacunajPotrosnju(tim);

                if (tim == Timovi.Honda)
                    port = 50000;
                else if (tim == Timovi.Mercedes)
                    port = 50001;
                else if (tim == Timovi.Ferari)
                    port = 50002;
                else if (tim == Timovi.Reno)
                    port = 50003;

                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Izabrali ste port: {port} - Tim: {bolid.Tim}\n");
                    chatBox.ScrollToEnd();
                });

                // Pokušaj da se poveže na server
                TcpKonekcija(port);
            }
            else
            {
                // Korisnik je zatvorio prozor bez izbora
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText("[INFO] Prozor za izbor tima je zatvoren.\n");
                    chatBox.ScrollToEnd();
                });
            }
        }

        // DAVID
        private bool PosaljiPorukuTcp(string poruka)
        {
            if (сокет != null && сокет.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(poruka);
                try
                {
                    int n = сокет.Send(data);
                    if (n == 0)
                        return false;
                    return true;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[GREŠKA pri slanju] {ex.Message}\n");
                        chatBox.ScrollToEnd();
                    });
                    return false;
                }
            }
            else
                return false;
        }
        private void ZahtevajTrkackiBroj()
        {
            if(trkacki_broj != string.Empty)
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Već imate trkački broj: {trkacki_broj}\n");
                    chatBox.ScrollToEnd();
                });
                return;
            }
            TcpKonekcija(59000);
            string tim;
            if(bolid.Tim == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Niste izabrali tim.\n");
                    chatBox.ScrollToEnd();
                });
                return;
            }
            switch (bolid.Tim)
            {
                case Timovi.Mercedes:
                    tim = "Mercedes";
                    break;
                case Timovi.Ferari:
                    tim = "Ferari";
                    break;
                case Timovi.Reno:
                    tim = "Reno";
                    break;
                case Timovi.Honda:
                    tim = "Honda";
                    break;
                default:
                    tim = "Nepoznat";
                    break;
            }
            if(PosaljiPorukuTcp(tim))
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Poslat zahtev za trkački broj: {tim}\n");
                    chatBox.ScrollToEnd();
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Ne mogu da pošaljem zahtev za trkački broj.\n");
                    chatBox.ScrollToEnd();
                });
            }

        }
        private void PosaljiVremeKruga(double vreme)
        {
            if(!PosaljiPorukuTcp(trkacki_broj + bolid.Tim + vreme.ToString()))
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Ne mogu da pošaljem vreme kruga.\n");
                    chatBox.ScrollToEnd();
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Poslato vreme kruga: {vreme}.\n");
                    chatBox.ScrollToEnd();
                });
            }
        }

        int br_sporog_kruga = 0;
        private double IzracunajVreme(int br_kruga)
        {
            double tempo_guma = 0,tempo_goriva = 0;
            switch(bolid.StanjeGuma)
            {
                case 0:
                    tempo_guma = 1.2 * br_kruga;
                    break;
                case 1:
                    tempo_guma = br_kruga;
                    break;
                case 2:
                    tempo_guma = 0.8 * br_kruga;
                    break;
            }
            tempo_goriva = 1 / bolid.StanjeGoriva;

            if (bolid.StanjeGuma < 35)
                tempo_guma -= 0.6;
            if(nacinVoznje == NacinVoznje.Brzo)
            {
                RacunajPotrosnju(bolid.Tim);
                bolid.PotrosnjaGuma += 0.3;
                bolid.PotrosnjaGoriva += 0.3;
            }
            else
            {
                RacunajPotrosnju(bolid.Tim);
            }
            if(osnovno_vreme == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Osnovno vreme nije postavljeno.\n");
                    chatBox.ScrollToEnd();
                });
                return 0;
            }
            double vreme = osnovno_vreme - tempo_guma - tempo_goriva;
            if (nacinVoznje == NacinVoznje.Sporo)
            {
                vreme += 0.2 * (++br_sporog_kruga);
            }
            return vreme;
        }
        private void ObavestiSilazak()
        {
            if (!PosaljiPorukuTcp("silazim sa staze"))
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Ne mogu da pošaljem obaveštenje o silasku sa staze.\n");
                    chatBox.ScrollToEnd();
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Poslato obaveštenje o silasku sa staze.\n");
                    chatBox.ScrollToEnd();
                });
            }
        }

        private void btZahtevajBroj_Click(object sender, RoutedEventArgs e)
        {
            ZahtevajTrkackiBroj();
        }
    }
}