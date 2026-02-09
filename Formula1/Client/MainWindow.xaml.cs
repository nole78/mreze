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
using System.Threading.Tasks;
using Client.PomocneFunkcije;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket сокет, UdpSoket, trkaTcpSoket;
        private CancellationTokenSource _cts, _cts2, _cts3, _cts4;
        private Task? _rxTask,_udpTask,_voziTask,_trkaTask;
        private KonfiguracijaAutomobila bolid = new KonfiguracijaAutomobila();
        private string trkacki_broj = "";
        private NacinVoznje nacinVoznje = NacinVoznje.Normalno;
        private double osnovno_vreme = 0, duzina_kruga = 0;
        private bool na_stazi = false, povezanSaGrazom = false, alarm_flag = false;
        private int br_sporog_kruga = 0;
        private const int garagePort = 50000, trkaPort = 59000;
        int port = -1,mojUdpPort = 0;

        public MainWindow()
        {
            _cts = new CancellationTokenSource();
            _cts2 = new CancellationTokenSource();
            _cts3 = new CancellationTokenSource();
            _cts4 = new CancellationTokenSource();
            UdpSoket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            сокет = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            trkaTcpSoket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            InitializeComponent();
            OtvoriOdabirTima();
        }
        private void OtvoriOdabirTima()
        {
            OdabirTima odabirTima = new OdabirTima();

            if (odabirTima.ShowDialog() == true)
            {
                Timovi tim = odabirTima.izabraniTim;
                bolid.Tim = tim;
                string poruka = "";
                var gume_gorivo = RacunanjePotrosnje.RacunajPotrosnju(bolid.Tim);
                bolid.PotrosnjaGuma = gume_gorivo.Item1;
                bolid.PotrosnjaGoriva = gume_gorivo.Item2;

                if (tim == Timovi.Honda)
                    poruka = "Honda";
                else if (tim == Timovi.Mercedes)
                    poruka = "Mercedes";
                else if (tim == Timovi.Ferari)
                    poruka = "Ferari";
                else if (tim == Timovi.Reno)
                    poruka = "Reno";

                Ispis($"[INFO] Izabrali ste tim: {poruka} - Tim: {bolid.Tim}");

                lbTim.Content = poruka;
                // Pokušaj da se poveže na server
                TcpKonekcija(garagePort, ref сокет);
                Loop(_cts, _rxTask, сокет);
                if(PosaljiPorukuTcp(poruka, сокет))
                {
                    Ispis("[INFO] Poruka poslana garaži: " + poruka);
                }
                else
                {
                    Ispis("[GREŠKA] Ne mogu da pošaljem poruku garaži.");
                }
            }
            else
            {
                // Korisnik je zatvorio prozor bez izbora
                Ispis("[INFO] Niste izabrali tim. Zatvaram aplikaciju.");
            }
        }
        public void Loop(CancellationTokenSource cts, Task? task,Socket soket)
        {
            cts = new CancellationTokenSource();
            task = Task.Run(() => ReceiveLoopTcp(cts.Token,soket));
        }
        private void TcpKonekcija(int port,ref Socket soket)
        {
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, port);
            try
            {
                soket.Connect(serverEP);
            }
            catch (Exception ex)
            {
                // Ispiši grešku umesto MessageBox
                Ispis("[GREŠKA] Ne mogu da se povežem: " + ex.Message);

                // Ponovo otvori prozor za izbor tima
                if(!povezanSaGrazom)
                    Dispatcher.Invoke(() =>
                    {
                        OtvoriOdabirTima();
                    });            
            }
        }
        private void ReceiveLoopTcp(CancellationToken token,Socket soket)
        {
            povezanSaGrazom = true;
            byte[] buf = new byte[4096];

            while (!token.IsCancellationRequested)
            {
                Socket? s = soket;
                if (s == null) break;
                try
                {
                    int n = s.Receive(buf);
                    if (n == 0)
                    {
                        Dispatcher.Invoke(() => Disconnect());
                        break;
                    }
                    EndPoint? ep = soket.RemoteEndPoint;
                    string posiljaocPort = (ep?.ToString() ?? "").Split(':')[1];
                    string text = Encoding.UTF8.GetString(buf, 0, n);

                    ObradiTCPPoruku(text,posiljaocPort);
                }
                catch (SocketException ex)
                {
                    Ispis("[GREŠKA pri primanju TCP] " + ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    Ispis("[GREŠKA] " + ex.Message);
                    break;
                }
            }
        }
        private void ObradiTCPPoruku(string poruka,string port_posiljaoca)
        {
            // Ako server odbije konekciju, otvori ponovo izbor tima
            if(port_posiljaoca == garagePort.ToString())
            {
                if (poruka.Contains("Nema više mesta u timu"))
                {
                    _cts.Cancel();
                    Dispatcher.Invoke(() =>
                    {
                        OtvoriOdabirTima();
                    });
                }
                if (poruka.Contains("Port:"))
                {
                    string port_text = poruka.Split(' ')[1].Trim();
                    if (!int.TryParse(port_text, out int portZaUdp) || portZaUdp <= garagePort || portZaUdp >= trkaPort)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            chatBox.AppendText($"[GREŠKA] Ne mogu da se povežem sa garažom.\n");
                            chatBox.ScrollToEnd();
                            OtvoriOdabirTima();
                        });
                    }
                    else
                    {
                        port = portZaUdp;
                        TcpKonekcija(trkaPort, ref trkaTcpSoket);
                        Loop(_cts4, _trkaTask, trkaTcpSoket);
                        povezanSaGrazom = true;
                        OtvoriUdpKonekciju();
                        Ispis($"[INFO] Povezan sa garazom. Port za UDP: {port}");
                    }
                }
            }
            else if (int.TryParse(poruka, out int broj) && broj >= 1 && broj <= 100)
            {
                trkacki_broj = broj.ToString();
                Dispatcher.Invoke(() =>
                {
                    lbTrkackiBroj.Content = trkacki_broj;
                });
                Ispis("[INFO] Dodeljen trkački broj: " + trkacki_broj);
            }
            else
            {
                Ispis("[INFO] Primljena poruka: " + poruka);
            }
        }
        private void Disconnect()
        {
            try
            {
                trkaTcpSoket.Close();
                сокет.Close();
                UdpSoket.Close();
                _cts.Cancel();
                _cts2.Cancel();
                _cts3.Cancel();
                _cts4.Cancel();
            }
            catch (Exception ex)
            {
                Ispis("[GREŠKA pri gašenju] " + ex.Message);
            }
        }
        public void ObradiUdpPoruku(string poruka)
        {
            Ispis("[UDP] Primljena poruka: " + poruka);
            if(poruka.Trim() == "Sidji sa staze")
            {
                ObavestiSilazak();
            }
            else if(poruka.Contains("Izlazak na stazu:"))
            {
                var niz = poruka.Split(' ');
                if (niz.Length == 4)
                {
                    var pom = niz[3].Split(',');
                    double gorivo = 0;
                    if(double.TryParse(pom[1], out gorivo) && gorivo >0)
                    {
                        switch(pom[0])
                        {
                            case "M":
                                bolid.StanjeGuma = 100;
                                bolid.StanjeGoriva = gorivo;
                                break;
                            case "S":
                                bolid.StanjeGuma = 80;
                                bolid.StanjeGoriva = gorivo;
                                break;
                            case "H":
                                bolid.StanjeGuma = 120;
                                bolid.StanjeGoriva = gorivo;
                                break;
                            default:
                                Ispis("[GREŠKA] Ne mogu da parsiram tip gume.\n");
                                return;
                        }
                        ZahtevajTrkackiBroj();
                        Thread.Sleep(100);
                        Vozi();
                    }
                }
            }
            else if(poruka == "Vozi brze" || poruka == "Vozi sporije" || poruka == "Vozi srednjim tempom")
            {
                switch (poruka)
                {
                    case "Vozi brze":
                        nacinVoznje = NacinVoznje.Brzo;
                        break;
                    case "Vozi sporije":
                        nacinVoznje = NacinVoznje.Sporo;
                        break;
                    case "Vozi srednjim tempom":
                        nacinVoznje = NacinVoznje.Normalno;
                        break;
                }
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Promenjen način vožnje: {poruka}\n");
                    chatBox.ScrollToEnd();
                });
            }
            else if (poruka.Contains("specifikacije kruga: "))
            {
                var niz = poruka.Trim().Split(' ');
                if (niz.Length == 4)
                {
                    if (double.TryParse(niz[2], out double duzina) && duzina > 0 && double.TryParse(niz[3],out double vreme) && vreme >= 10)
                    {
                        duzina_kruga = duzina;
                        osnovno_vreme = vreme;
                        Dispatcher.Invoke(() =>
                        {
                            chatBox.AppendText($"[INFO] Postavljene specifikacije kruga: dužina={duzina}m, osnovno vreme={vreme}s\n");
                            chatBox.ScrollToEnd();
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            chatBox.AppendText($"[GREŠKA] Ne mogu da parsiram dužinu kruga.\n");
                            chatBox.ScrollToEnd();
                        });
                    }
                }
            }

        }
        private void ReceiveLoopUdp(CancellationToken token,EndPoint senderEP)
        {
            byte[] buf = new byte[4096];
            while (!token.IsCancellationRequested)
            {
                Socket? s = UdpSoket;
                if (s == null) 
                    break;
                try
                {
                    int n = s.ReceiveFrom(buf,ref senderEP);
                    if (n == 0)
                    {
                        break;
                    }

                    string text = Encoding.UTF8.GetString(buf, 0, n);
                    ObradiUdpPoruku(text);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    Thread.Sleep(10);
                    continue;
                }
                catch (SocketException ex)
                {
                    Ispis("[GREŠKA pri primanju UDP] " + ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    Ispis("[GREŠKA] " + ex.Message);
                    break;
                }
            }
        }
        private void OtvoriUdpKonekciju()
        {
            try
            {
                IPEndPoint destinationEP = new IPEndPoint(IPAddress.Any, 0);
                UdpSoket.Bind(destinationEP);
                IPEndPoint? mojEP = (IPEndPoint?)UdpSoket.LocalEndPoint;
                if(mojEP != null)
                    mojUdpPort = mojEP.Port;
                UdpSoket.Blocking = false;
                _cts2 = new CancellationTokenSource();
                _udpTask = Task.Run(() => ReceiveLoopUdp(_cts2.Token, destinationEP));

                Ispis("UDP utičnica otvorena na portu: " + mojUdpPort); ;
                PosaljiPorukuTcp("UDP_PORT: " + mojUdpPort, сокет);
            }
            catch { }
        }
        private bool PosaljiPorukuTcp(string poruka,Socket soket)
        {
            if (soket != null && soket.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(poruka);
                try
                {
                    int n = soket.Send(data);
                    if (n == 0)
                        return false;
                    return true;
                }
                catch (Exception ex)
                {
                    Ispis($"[GREŠKA pri slanju TCP] {ex.Message}");
                    return false;
                }
            }
            else
                return false;
        }
        private bool PosaljiPorukuUdp(string poruka)
        {
            if (UdpSoket != null)
            {
                byte[] data = Encoding.UTF8.GetBytes(poruka);
                IPEndPoint destinationEP = new IPEndPoint(IPAddress.Loopback, port);
                try
                {
                    int n = UdpSoket.SendTo(data, destinationEP);
                    if (n == 0)
                        return false;
                    return true;
                }
                catch (Exception ex)
                {
                    Ispis("[GREŠKA pri slanju UDP] " + ex.Message);
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
                Ispis("[GREŠKA] Već imate trkački broj.");
                return;
            }
            string tim;
            if(bolid.Tim == 0)
            {
                Ispis("[GREŠKA] Niste izabrali tim");
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
            if (PosaljiPorukuTcp(tim,trkaTcpSoket))
            {
                Ispis("[INFO] Zahtev za trkački broj poslat.");
            }
            else
            {
                Ispis("[GREŠKA] Ne mogu da pošaljem zahtev za trkački broj.");
            }

        }
        private void PosaljiVremeKruga(double vreme)
        {
            if(!PosaljiPorukuTcp(trkacki_broj + bolid.Tim + " " + vreme.ToString(),trkaTcpSoket))
            {
                Ispis("[GREŠKA] Ne mogu da pošaljem vreme kruga.");
            }
            else
            {
                Ispis("[INFO] Poslato vreme kruga: " + vreme);
            }
        }
        private double IzracunajVreme(int br_kruga)
        {
            if (osnovno_vreme == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Osnovno vreme nije postavljeno.\n");
                    chatBox.ScrollToEnd();
                });
                return 0;
            }
            double tempo_guma = 0, tempo_goriva = 0, vreme = 0;
            switch (bolid.StanjeGuma)
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
            double procenat_guma = bolid.StanjeGuma / bolid.GetTrajanjeGuma() * 100;
            if ( procenat_guma < 35 )
                tempo_guma -= 0.6;
            bolid.StanjeGuma -= duzina_kruga * bolid.PotrosnjaGuma;
            bolid.StanjeGoriva -= duzina_kruga * bolid.PotrosnjaGoriva;
            var gume_gorivo = RacunanjePotrosnje.RacunajPotrosnju(bolid.Tim);

            if (nacinVoznje == NacinVoznje.Brzo)
            {
                bolid.PotrosnjaGuma = gume_gorivo.Item1 + 0.3;
                bolid.PotrosnjaGoriva = gume_gorivo.Item2 + 0.3;
                br_sporog_kruga = 0;
            }
            else
            {
                bolid.PotrosnjaGuma = gume_gorivo.Item1;
                bolid.PotrosnjaGoriva = gume_gorivo.Item2;
                if(nacinVoznje == NacinVoznje.Normalno)
                    br_sporog_kruga = 0;
            }
            vreme = osnovno_vreme - tempo_guma - tempo_goriva;
            if (nacinVoznje == NacinVoznje.Sporo)
            {
                vreme += 0.2 * (++br_sporog_kruga);
            }

            bolid.StanjeGoriva -= duzina_kruga * bolid.PotrosnjaGoriva;
            bolid.StanjeGuma -= duzina_kruga * bolid.PotrosnjaGuma;
            if(procenat_guma <= 25 || bolid.StanjeGoriva <= 2 * duzina_kruga * bolid.PotrosnjaGoriva)
            {
                alarm_flag = true;
            }
            return vreme;
        }
        private void VoziLoop(CancellationToken token)
        {
            int krug = 1;
            while (!token.IsCancellationRequested)
            {
                double vreme_kruga = IzracunajVreme(krug);
                if (vreme_kruga == 0)
                    break;
                krug++;
                if (alarm_flag) // alarm
                {
                    ObavestiSilazak();
                }
                Thread.Sleep((int)(vreme_kruga * 1000)); // Simulacija vremena kruga
                PosaljiVremeKruga(vreme_kruga);
                if (PosaljiPorukuUdp("gume: " + bolid.PotrosnjaGuma + " gorivo: " + bolid.PotrosnjaGoriva))
                {
                    Ispis("[INFO] Poslato stanje guma i goriva.");
                }
            }
            if (!PosaljiPorukuTcp("silazim sa staze", trkaTcpSoket))
            {
                Ispis("[GREŠKA] Ne mogu da pošaljem poruku silaska sa staze.");
            }
            else
            {
                Ispis("[INFO] Poslato obaveštenje o silasku sa staze.");
            }
        }
        private void Vozi()
        {
            if (trkacki_broj == string.Empty)
            {
                Ispis("[GREŠKA] Nemate trkački broj.");
                return;
            }
            if(na_stazi)
            {
                Ispis("[GREŠKA] Već ste na stazi.");
                return;
            }
            PosaljiPorukuTcp("izlazim na stazu", trkaTcpSoket);
            na_stazi = true;
            _cts3 = new CancellationTokenSource();
            _voziTask = Task.Run(() => VoziLoop(_cts3.Token));
        }
        private void ObavestiSilazak()
        {
            if (!na_stazi)
                return;
            na_stazi = false;
            if(_cts3 != null)
                _cts3?.Cancel();
            else
            {
                Ispis("[GREŠKA] Nije pokrenuta vožnja, ne mogu da se zaustavim.");
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            _cts2.Cancel();
            _cts3.Cancel();
            _cts4.Cancel();
            _cts.Cancel();
            trkaTcpSoket.Close();
            UdpSoket.Close();
            сокет.Close();
            base.OnClosed(e);
        }
        private void Ispis(string txt)
        {
            Dispatcher.Invoke(() =>
            {
                chatBox.AppendText(txt + "\n");
                chatBox.ScrollToEnd();
            });
        }
    }
}