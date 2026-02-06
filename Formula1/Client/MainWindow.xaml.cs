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
using Common;
namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket сокет;
        private Socket UdpSoket;
        private CancellationTokenSource _cts;
        private Task _rxTask;
        private KonfiguracijaAutomobila bolid = new KonfiguracijaAutomobila();

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
                OtvoriOdabirTima();
            }
        }

        private void ReceiveLoopTcp(CancellationToken token)
        {
            byte[] buf = new byte[4096];

            while (!token.IsCancellationRequested)
            {
                Socket s = сокет;
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
            Dispatcher.Invoke(() =>
            {
                chatBox.AppendText(poruka + "\n");
                chatBox.ScrollToEnd();
            });

            // Ako server odbije konekciju, otvori ponovo izbor tima
            if (poruka.Contains("Nema više mesta u timu"))
            {
                Dispatcher.Invoke(() =>
                {
                    OtvoriOdabirTima();
                });
            }else
            {
                OtvoriUdpKonekciju();
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

        private void OtvoriOdabirTima()
        {
            OdabirTima odabirTima = new OdabirTima();
            
            if (odabirTima.ShowDialog() == true)
            {
                Timovi tim = odabirTima.izabraniTim;
                bolid.Tim = tim;
                
                if(tim == Timovi.Mercedes)
                {
                    bolid.PotrosnjaGuma = 0.3;
                    bolid.PotrosnjaGoriva = 0.6;
                }
                else if(tim == Timovi.Ferari)
                {
                    bolid.PotrosnjaGuma = 0.3;
                    bolid.PotrosnjaGoriva = 0.5;
                }
                else if (tim == Timovi.Reno)
                {
                    bolid.PotrosnjaGuma = 0.4;
                    bolid.PotrosnjaGoriva = 0.7;
                }
                else if(tim == Timovi.Honda)
                {
                    bolid.PotrosnjaGuma = 0.2;
                    bolid.PotrosnjaGoriva = 0.6;
                }

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
    }
}