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

namespace Server
{
    public partial class MainWindow : Window
    {
        private Dictionary<string, List<double>> vremena = new Dictionary<string, List<Double>>();
        private Dictionary<string, double> najbolja_vremena = new Dictionary<string, double>();
        private static List<string> trkackiBrojevi = new List<string>();
        private static List<string> timovi = new List<string>() { "reno", "mercedes", "ferari", "honda" };
        private static Random rand = new Random();
        private int brojVozacaNaStazi = 0;

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

                btStart.IsEnabled = true;
                btStop.IsEnabled = false;
                ispis("Server zaustavljen");
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
                ispis("Novi klijent: " + str);
                SendToClient(klijent, "Dobrodošao! Povezan si kao " + str);
            }
            catch (SocketException)
            {
                // ignore (non-blocking accept race)
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => { 
                    MessageBox.Show("GREŠKA", "Greška pri prihvaćanju klijenta: " + ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); 
                });
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
                ispis("[" + str + "] " + text);

                if (timovi.Contains(text.Trim().ToLower()))
                {
                    TrazenjeTrkackogBroja(klijent, text.Trim().ToLower());
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
                    if (vozac != string.Empty && double.TryParse(niz[1], out vreme) && vreme > 10 && vreme < 1000)
                    {
                        VremeKruga(vozac, vreme);
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("GREŠKA", "Neispravno vreme kruga od klijenta " + str, MessageBoxButton.OK, MessageBoxImage.Error);
                        });
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
                ispis("Klijent " + str + " uklonjen (" + razlog + ")");
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
                server_socket.Bind(new IPEndPoint(IPAddress.Any, port));
                server_socket.Listen(100);
                server_socket.Blocking = false;

                _cts = new CancellationTokenSource();
                server_task = Task.Run(() => ServerLoop(_cts.Token));

                btStart.IsEnabled = false;
                btStop.IsEnabled = true;
                ispis("Server pokrenut na portu " + port);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => { 
                    MessageBox.Show("GREŠKA", "Greška pri pokretanju servera: " + ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); 
                });
            }
        }

        private void btStart_Click(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        private void btStop_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
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

                ispis("Dodeljen trkački broj: " + broj + " klijentu " + str);
            }
            else
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("GREŠKA", "Greška pri slanju trkačkog broja klijentu.", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        private void SilazakSaStaze(Socket klijent)
        {
            brojVozacaNaStazi--;
            klijentiNaStazi.Remove(klijent);
            if (brojVozacaNaStazi < 0)
            {
                brojVozacaNaStazi = 0;
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("GREŠKA", "Broj vozača na stazi ne može biti negativan.", MessageBoxButton.OK, MessageBoxImage.Error);
                });
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
                tbVremena.Clear();
            });
            foreach (var krug in vremena)
            {
                string vozac = krug.Key;
                List<double> vremenaKrugova = krug.Value;
                foreach (double vreme in vremenaKrugova)
                {
                    var match = Regex.Match(vozac, @"^(\d+)([a-zA-Z]+)$");
                    Dispatcher.Invoke(() =>
                    {
                        tbVremena.Text += match.Groups[2].Value + " " + match.Groups[1].Value + " : " + (int)vreme / 60 + ":" + (int)vreme % 60 + ":" + (int)(vreme * 100) % 100 + "\n";
                    });
                }
            }
            Dispatcher.Invoke(() =>
            {
                tbNajVremena.Clear();
            });
            foreach (var krug in najbolja_vremena)
            {
                string vozac = krug.Key;
                double vreme = krug.Value;
                var match = Regex.Match(vozac, @"^(\d+)([a-zA-Z]+)$");
                Dispatcher.Invoke(() =>
                {
                    tbNajVremena.Text += match.Groups[2].Value + " " + match.Groups[1].Value + " : " + (int)vreme / 60 + ":" + (int)vreme % 60 + ":" + (int)(vreme * 100) % 100 + "\n";
                });
            }
        }

        private void VremeKruga(string vozac, double vreme)
        {
            if (!vremena.TryAdd(vozac, new List<double>() { vreme }))
            {
                vremena[vozac].Add(vreme);
            }
            ispis("Vreme kruga za vozača " + vozac + ": " + Double.Round(vreme, 2) + " sekundi");
            if (!najbolja_vremena.ContainsKey(vozac))
            {
                najbolja_vremena.Add(vozac, vreme);
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
            }

        }

        void ispis(string str)
        {
            Dispatcher.Invoke(() =>
            {
                tbTEST.Text += str + "\n";
            });
        }

    }
}