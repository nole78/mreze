using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;
using Server.Entiteti;

namespace Server
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Vreme> ListaVremena { get; set; } = new();
        private Dictionary<string, List<double>> vremena = new Dictionary<string, List<Double>>();
        private Dictionary<string, double> najbolja_vremena = new Dictionary<string, double>();
        private static List<string> trkackiBrojevi = new List<string>();
        private static List<string> timovi = new List<string>() { "reno", "mercedes", "ferari", "honda" };
        private static Random rand = new Random();
        private int brojVozacaNaStazi = 0;
        private double najbrzeVreme = 0;

        private Socket? server_socket;
        private readonly List<Socket> klienti = new List<Socket>();
        private readonly List<Socket> klijentiNaStazi = new List<Socket>();
        private readonly object _lock = new object();

        private CancellationTokenSource? _cts;
        private Task? server_task;

        public MainWindow()
        {
            InitializeComponent();
            StartServer();
            DataContext = this;
        }

        private void StopServer()
        {
            try
            {
                if (_cts != null) _cts.Cancel();

                lock (_lock)
                {
                    for (int i = 0; i < klienti.Count; i++)
                        SafeClose(klienti[i]);
                    klienti.Clear();
                }

                SafeClose(server_socket);
                server_socket = null;
                Ispis("Server zaustavljen");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("GREŠKA", "Greška pri zaustavljanju servera: " + ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        private void ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Socket? listener = server_socket;
                if (listener == null)
                {
                    Thread.Sleep(50);
                    continue;
                }

                List<Socket> soketi = new List<Socket>();
                lock (_lock)
                {
                    soketi.Add(listener);
                    soketi.AddRange(klienti);
                }

                try
                {
                    // 200ms timeout (microseconds)
                    Socket.Select(soketi, null, null, 200000);
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < soketi.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    Socket s = soketi[i];
                    if (s == listener)
                        AcceptClient(listener);
                    else
                        ReceiveFromClient(s);
                }
            }
        }
        private void AcceptClient(Socket listener)
        {
            try
            {
                Socket klijent = listener.Accept();
                klijent.Blocking = false;

                lock (_lock) klienti.Add(klijent);

                string? str = (klijent.RemoteEndPoint != null) ? klijent.RemoteEndPoint.ToString() : "unknown";
                Ispis("Novi klijent: " + str);
                SendToClient(klijent, "Dobrodošao! Povezan si kao " + str);
            }
            catch (SocketException)
            {
                // ignore (non-blocking accept race)
            }
            catch (Exception ex)
            {
                Ispis("GREŠKA: Greška pri prihvaćanju klijenta: " + ex.Message);
            }
        }
        private void ReceiveFromClient(Socket klijent)
        {
            byte[] buffer = new byte[1024];

            try
            {
                int n = klijent.Receive(buffer);
                if (n == 0)
                {
                    RemoveClient(klijent, "Disconnected");
                    return;
                }

                string text = Encoding.UTF8.GetString(buffer, 0, n);
                string? str = (klijent.RemoteEndPoint != null) ? klijent.RemoteEndPoint.ToString() : "klijent";
                Ispis("[" + str + "] " + text);

                if (timovi.Contains(text.Trim().ToLower()))
                {
                    TrazenjeTrkackogBroja(klijent, text.Trim());
                }
                else if (text.Trim().ToLower() == "silazim sa staze")
                {
                    SilazakSaStaze(klijent);
                }
                else if (text.Contains("Mercedes") || text.Contains("Reno") || text.Contains("Ferari") || text.Contains("Honda"))
                {
                    var niz = text.Split(' ');
                    string vozac = niz[0];
                    double vreme = 0;
                    if (vozac != string.Empty && double.TryParse(niz[1], out vreme) && vreme > 1 && vreme < 1000)
                    {
                        VremeKruga(vozac, vreme);
                    }
                    else
                    {
                        Ispis("Neispravno vreme kruga od klijenta " + str + ": " + niz[1]);
                        RemoveClient(klijent, "Invalid lap time");
                        return;
                    }

                }
                else if (text.Trim() == "izlazim na stazu" && !klijentiNaStazi.Contains(klijent))
                {
                    klijentiNaStazi.Add(klijent);
                    brojVozacaNaStazi++;
                }

            }
            catch (SocketException ex)
            {
                RemoveClient(klijent, "SocketException: " + ex.SocketErrorCode);
            }
            catch (Exception ex)
            {
                RemoveClient(klijent, "Error: " + ex.Message);
            }
        }
        private bool SendToClient(Socket client, string msg)
        {
            try
            {
                int n = client.Send(Encoding.UTF8.GetBytes(msg));
                if (n == 0)
                    return false;
                return true;
            }
            catch
            {
                RemoveClient(client, "Send failed");
                return false;
            }
        }
        private void RemoveClient(Socket klijent, string razlog)
        {
            bool removed = false;
            lock (_lock)
            {
                removed = klienti.Remove(klijent);
            }

            if (removed)
            {
                string? str = (klijent.RemoteEndPoint != null) ? klijent.RemoteEndPoint.ToString() : "unknown";
                Ispis("Klijent " + str + " uklonjen (" + razlog + ")");
            }

            SafeClose(klijent);
        }
        private static void SafeClose(Socket? s)
        {
            if (s == null) return;
            try { s.Shutdown(SocketShutdown.Both); } catch { }
            try { s.Close(); } catch { }
        }
        protected override void OnClosed(EventArgs e)
        {

            StopServer();
            base.OnClosed(e);
        }
        private void StartServer()
        {
            int port = 59000;
            try
            {
                server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server_socket.Bind(new IPEndPoint(IPAddress.Any,port));
                server_socket.Listen(100);
                server_socket.Blocking = false;

                _cts = new CancellationTokenSource();
                server_task = Task.Run(() => ServerLoop(_cts.Token));
                Ispis("Server pokrenut\n\tAddress: \t" + (server_socket.LocalEndPoint?.ToString() ?? " ").Split(':')[0] + "\n\tPort: \t\t" + (server_socket.LocalEndPoint?.ToString() ?? " ").Split(':')[1]);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    Ispis("GREŠKA: Ne mogu pokrenuti server: " + ex.Message);
                });
            }
        }
        private void TrazenjeTrkackogBroja(Socket klijent, string tim)
        {
            string broj = rand.Next(1, 99).ToString();
            while (trkackiBrojevi.Contains(broj))
            {
                broj = rand.Next(1, 99).ToString();
            }
            trkackiBrojevi.Add(broj);
            if (SendToClient(klijent, broj))
            {
                string vozac = broj + tim;
                vremena[vozac] = new List<double>();
                string? str = (klijent.RemoteEndPoint != null) ? klijent.RemoteEndPoint.ToString() : "klijent";

                Ispis("Dodeljen trkački broj: " + broj + " klijentu " + str);
            }
            else
            Dispatcher.Invoke(() =>
            {
                Ispis("GREŠKA: Ne mogu poslati trkački broj klijentu.");
            });
        }
        private void SilazakSaStaze(Socket klijent)
        {
            brojVozacaNaStazi--;
            klijentiNaStazi.Remove(klijent);
            if (brojVozacaNaStazi < 0)
            {
                brojVozacaNaStazi = 0;
                Ispis("Broj vozača na stazi ne može biti negativan. Resetovan na 0.");
            }
            if (brojVozacaNaStazi == 0)
            {
                IspisVremena();
            }
        }
        private void IspisVremena()
        {
            Dispatcher.Invoke(() =>
            {
                ListaVremena.Clear();
            });
            bool najbrzi_generalno_prikazan = false;
            foreach (var krug in vremena)
            {
                bool najbrzi_krug_prikazan = false;
                string vozac = krug.Key;
                var vremenaKrugova = krug.Value;
                double najbolje_vreme = 0;
                najbolje_vreme = najbolja_vremena[vozac];
                foreach (var vreme in vremenaKrugova)
                {
                    int minut = (int)vreme / 60;
                    int sekunde = (int)vreme % 60;
                    int milisekunde = (int)(vreme * 10000)%10000;
                    string ispis_vreme = minut + ":" + sekunde + ":" + milisekunde;
                    Dispatcher.Invoke(() =>
                    { 
                        ListaVremena.Add(new Vreme
                        {
                            Vozac = vozac,
                            VremeKruga = ispis_vreme,
                            Najbrze = vreme == najbolje_vreme && !najbrzi_krug_prikazan,
                            NajbrzeGeneralno = vreme == najbrzeVreme && !najbrzi_generalno_prikazan
                        });
                    });
                    if (vreme == najbolje_vreme && !najbrzi_krug_prikazan)
                        najbrzi_krug_prikazan = true;
                    if (vreme == najbrzeVreme && !najbrzi_generalno_prikazan)
                        najbrzi_generalno_prikazan = true;
                }
            }
            Dispatcher.Invoke(() =>
            {
                Lista.Dispatcher.Invoke(() =>
                {
                    CollectionViewSource.GetDefaultView(Lista.ItemsSource)?.Refresh();
                });

            });
        }
        private void VremeKruga(string vozac, double vreme)
        {
            if (!vremena.TryAdd(vozac, new List<double>() { vreme }))
            {
                vremena[vozac].Add(vreme);
            }
            if (!najbolja_vremena.ContainsKey(vozac))
            {
                najbolja_vremena.Add(vozac, vreme);
                if(najbrzeVreme == 0 || vreme < najbrzeVreme)
                {
                    najbrzeVreme = vreme;
                }
            }
            else
            {
                double trenutnoNajbolje = vreme;
                foreach (double v in vremena[vozac])
                {
                    if (v < trenutnoNajbolje)
                    {
                        trenutnoNajbolje = v;
                    }
                }
                najbolja_vremena[vozac] = trenutnoNajbolje;
                if (najbrzeVreme == 0 || trenutnoNajbolje < najbrzeVreme)
                {
                    najbrzeVreme = trenutnoNajbolje;
                }
            }

        }
        void Ispis(string str)
        {
            Dispatcher.Invoke(() =>
            {
                tbTEST.Text += "\n" + str;
            });
        }
    }
}