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

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket? сокет, UdpSoket, trkaTcpSoket;
        private CancellationTokenSource? _cts, _cts2, _cts3, _cts4;
        private Task? _rxTask,_udpTask,_voziTask,_trkaTask;
        private KonfiguracijaAutomobila bolid = new KonfiguracijaAutomobila();
        private string? trkacki_broj = "";
        private NacinVoznje nacinVoznje = NacinVoznje.Normalno;
        private double osnovno_vreme = 0, duzina_kruga;
        private bool na_stazi = false, povezanSaGrazom = false;
        private int br_sporog_kruga = 0;
        private const int garagePort = 50000, trkaPort = 59000;
        int port = 0;

        public MainWindow()
        {
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
                RacunajPotrosnju(tim);

                if (tim == Timovi.Honda)
                    poruka = "Honda";
                else if (tim == Timovi.Mercedes)
                    poruka = "Mercedes";
                else if (tim == Timovi.Ferari)
                    poruka = "Ferari";
                else if (tim == Timovi.Reno)
                    poruka = "Reno";

                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[INFO] Izabrali ste tim: {poruka} - Tim: {bolid.Tim}\n");
                    chatBox.ScrollToEnd();
                });

                // Pokušaj da se poveže na server
                TcpKonekcija(garagePort, ref сокет);
                Loop(_cts, _rxTask, сокет);
                if(PosaljiPorukuTcp(poruka, сокет))
                {
                    Dispatcher.Invoke(()=>
                    {
                        chatBox.AppendText($"[INFO] Poslali ste poruku garazi: {poruka}\n");
                        chatBox.ScrollToEnd();
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[INFO] Neuspesno slanje poruke garazi.\n");
                        chatBox.ScrollToEnd();
                    });
                }
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
        public void Loop(CancellationTokenSource cts, Task task,Socket soket)
        {
            cts = new CancellationTokenSource();
            task = Task.Run(() => ReceiveLoopTcp(cts.Token,soket));
        }
        private void TcpKonekcija(int port,ref Socket soket)
        {
            soket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, port);
            try
            {
                soket.Connect(serverEP);
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
                    EndPoint ep = soket.RemoteEndPoint;
                    string posiljaocPort = ep.ToString().Split(':')[1];

                    string text = Encoding.UTF8.GetString(buf, 0, n);

                    ObradiPoruku(text,posiljaocPort);
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
                        chatBox.AppendText($"[GREŠKA123] {ex.Message}\n");
                        chatBox.ScrollToEnd();
                        Disconnect();
                    });
                    break;
                }
            }
        }
        private void ObradiPoruku(string poruka,string port_posiljaoca)
        {
            // Ako server odbije konekciju, otvori ponovo izbor tima
            int broj;
            if(port_posiljaoca == garagePort.ToString())
            {
                if (poruka.Contains("Nema više mesta u timu"))
                {
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
                        Dispatcher.Invoke(() =>
                        {
                            chatBox.AppendText($"[INFO] Povezan sa garažom. Port za UDP: {port}\n");
                            chatBox.ScrollToEnd();
                        });
                    }
                }
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
        public void ObradiUdpPoruku(string poruka)
        {
            Dispatcher.Invoke(() =>
            {
                chatBox.AppendText($"[UDP] {poruka}\n");
                chatBox.ScrollToEnd();
            });
            if(poruka == "sidji sa staze")
            {
                ObavestiSilazak();
            }
            else if(poruka.Contains("Izlazak na stazu"))
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
                                bolid.StanjeGuma = 70;
                                bolid.StanjeGoriva = gorivo;
                                break;
                            case "H":
                                bolid.StanjeGuma = 50;
                                bolid.StanjeGoriva = gorivo;
                                break;
                            default:
                                Dispatcher.Invoke(() =>
                                {
                                    chatBox.AppendText($"[GREŠKA] Ne mogu da parsiram tip gume.\n");
                                    chatBox.ScrollToEnd();
                                });
                                return;
                        }
                        Vozi();
                    }
                }
            }
            else if(poruka == "brzo" || poruka == "sporo" || poruka == "normalno")
            {
                switch (poruka)
                {
                    case "brzo":
                        nacinVoznje = NacinVoznje.Brzo;
                        break;
                    case "sporo":
                        nacinVoznje = NacinVoznje.Sporo;
                        break;
                    case "normalno":
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
                var niz = poruka.Split(' ');
                if (niz.Length == 4)
                {
                    if (double.TryParse(niz[3], out double duzina) && duzina > 0 && double.TryParse(niz[4],out double vreme) && osnovno_vreme > 10)
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
                        //Dispatcher.Invoke(() => Disconnect());
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
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[GREŠKA - SocketException] {ex.Message}\n");
                        chatBox.ScrollToEnd();
                        //Disconnect();
                    });
                    break;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[GREŠKA] {ex.Message}\n");
                        chatBox.ScrollToEnd();
                        //Disconnect();
                    });
                    break;
                }
            }
        }
        private void OtvoriUdpKonekciju()
        {
            UdpSoket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint destinationEP = new IPEndPoint(IPAddress.Loopback, port);
            UdpSoket.Bind(destinationEP);
            UdpSoket.Blocking = false;
            _cts2 = new CancellationTokenSource();
            _udpTask = Task.Run(() => ReceiveLoopUdp(_cts2.Token, destinationEP));

            Dispatcher.Invoke(() =>
            {
                chatBox.AppendText($"Otvorena UDP utičnica");
                chatBox.ScrollToEnd();
            });

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
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[GREŠKA pri slanju UDP] {ex.Message}\n");
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
            if (PosaljiPorukuTcp(tim,trkaTcpSoket))
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
            if(!PosaljiPorukuTcp(trkacki_broj + bolid.Tim + vreme.ToString(),trkaTcpSoket))
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
        private double IzracunajVreme(int br_kruga)
        {
            double vreme = 0;
                double tempo_guma = 0, tempo_goriva = 0;
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

                if (bolid.StanjeGuma < 35)
                    tempo_guma -= 0.6;
                if (nacinVoznje == NacinVoznje.Brzo)
                {
                    RacunajPotrosnju(bolid.Tim);
                    bolid.PotrosnjaGuma += 0.3;
                    bolid.PotrosnjaGoriva += 0.3;
                }
                else
                {
                    RacunajPotrosnju(bolid.Tim);
                }
                if (osnovno_vreme == 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        chatBox.AppendText($"[GREŠKA] Osnovno vreme nije postavljeno.\n");
                        chatBox.ScrollToEnd();
                    });
                    return 0;
                }
                vreme = osnovno_vreme - tempo_guma - tempo_goriva;
                if (nacinVoznje == NacinVoznje.Sporo)
                {
                    vreme += 0.2 * (++br_sporog_kruga);
                }

                bolid.StanjeGoriva -= duzina_kruga * bolid.PotrosnjaGoriva;
                bolid.StanjeGuma -= duzina_kruga * bolid.PotrosnjaGuma;
            return vreme;
        }
        private void VoziLoop(CancellationToken token)
        {
            na_stazi = true;
            int krug = 1;
            while (!token.IsCancellationRequested)
            {
                double vreme_kruga = IzracunajVreme(krug);
                if (vreme_kruga == 0)
                    break;
                PosaljiVremeKruga(vreme_kruga);
                 krug++;
                Thread.Sleep((int)(vreme_kruga*1000)); // Simulacija vremena kruga
                PosaljiVremeKruga(vreme_kruga);
                PosaljiPorukuUdp("gume: " + bolid.PotrosnjaGuma + " gorivo: " + bolid.PotrosnjaGoriva);
            }
        }
        private void Vozi()
        {
            if (trkacki_broj == string.Empty)
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Nemate trkački broj.\n");
                    chatBox.ScrollToEnd();
                });
                return;
            }
            if(na_stazi)
            {
                Dispatcher.Invoke(() =>
                {
                    chatBox.AppendText($"[GREŠKA] Već ste na stazi.\n");
                    chatBox.ScrollToEnd();
                });
                return;
            }
            _cts3 = new CancellationTokenSource();
            _voziTask = Task.Run(() => VoziLoop(_cts3.Token));
        }
        private void ObavestiSilazak()
        {
            if (!na_stazi)
                return;
            if (!PosaljiPorukuTcp("silazim sa staze",trkaTcpSoket))
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
        protected override void OnClosed(EventArgs e)
        {
            UdpSoket?.Close();
            сокет?.Close();
            base.OnClosed(e);
        }
    }
}