using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Client.Modeli;
using Client.Enumeracije;
namespace ClientServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        struct Trkaci
        {
            public Timovi tim;
            public EndPoint ep;
        }
        private List<Trkaci> Honda = new List<Trkaci>();
        private List<Trkaci> Mercedes = new List<Trkaci>();
        private List<Trkaci> Ferari = new List<Trkaci>();
        private List<Trkaci> Reno = new List<Trkaci>();
        
        private readonly List<Socket> _listeners = new List<Socket>();
        private readonly List<Socket> _clients = new List<Socket>();
        private readonly object _lock = new object();

        private CancellationTokenSource _cts;
        private List<Task> _serverTasks;
        private Socket udpSocket = null;

        private readonly int[] _ports = { 50000, 50001, 50002, 50003};
        private int[] povezani = { 0, 0, 0, 0 };

        private int DuzinaStaze = 0;
        private double OsnovnoVremeKruga = 0;
        public MainWindow()
        {
            InitializeComponent();
            PrikaziUnosParametaraTrka();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        private async void StartServer()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _serverTasks = new List<Task>();

                foreach (var port in _ports)
                {
                    var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    
                    
                    Dispatcher.Invoke(() =>
                    {
                        konzolaGaraza.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + "Server START na portu " + port + "\n");
                        konzolaGaraza.ScrollToEnd();
                    });
                    
                    listener.Bind(new IPEndPoint(IPAddress.Any, port));
                    listener.Listen(100);
                    
                    _listeners.Add(listener);
                    
                    var task = AcceptClientsAsync(listener, port, _cts.Token);
                    _serverTasks.Add(task);
                }

                await Task.WhenAll(_serverTasks);
            }
            catch (Exception ex)
            {
                Ispisi($"Server greška: {ex.Message}");
            }
        }

        private async Task AcceptClientsAsync(Socket listener, int port, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Socket clientSocket = await listener.AcceptAsync();

                    // Provjeri broj povezanih klijenta za ovaj port
                    int portIndex = port - 50000;

                    if (povezani[portIndex] >= 2)
                    {
                       
                        var odbijPoruka = Encoding.UTF8.GetBytes("Nema više mesta u timu");
                        try
                        {
                            await clientSocket.SendAsync(new ArraySegment<byte>(odbijPoruka), SocketFlags.None);
                        }
                        catch { }

                        clientSocket.Close();

                        Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] Konekcija odbijena - tim je pun!");

                        continue;  // Nastavi sa čekanjem sledećeg klijenta
                    }

                    lock (_lock)
                    {
                        _clients.Add(clientSocket);
                    }

                    var clientEndPoint = clientSocket.RemoteEndPoint;
                    if(udpSocket == null)
                    {
                        OtvoriUdpSoket(cancellationToken);
                    }
                    
                   
                    povezani[portIndex]++;

                    if (portIndex == 0)  // Honda — port 50000
                    {
                        Trkaci trkaci = new Trkaci();
                        trkaci.tim = Timovi.Honda;
                        trkaci.ep = clientEndPoint;
                        Honda.Add(trkaci);
                        int idx = Honda.Count;
                        EnableAutoButtonForTeam(0, idx);
                    }
                    else if(portIndex == 1)  // Mercedes — port 50001
                    {
                        Trkaci trkaci = new Trkaci();
                        trkaci.tim = Timovi.Mercedes;
                        trkaci.ep = clientEndPoint;
                        Mercedes.Add(trkaci);
                        int idx = Mercedes.Count;
                        EnableAutoButtonForTeam(1, idx);
                    }
                    else if (portIndex == 2)  // Ferari — port 50002
                    {
                        Trkaci trkaci = new Trkaci();
                        trkaci.tim = Timovi.Ferari;
                        trkaci.ep = clientEndPoint;
                        Ferari.Add(trkaci);
                        int idx = Ferari.Count;
                        EnableAutoButtonForTeam(2, idx);
                    }
                    else if (portIndex == 3)  // Reno — port 50003
                    {
                        Trkaci trkaci = new Trkaci();
                        trkaci.tim = Timovi.Reno;
                        trkaci.ep = clientEndPoint;
                        Reno.Add(trkaci);
                        int idx = Reno.Count;
                        EnableAutoButtonForTeam(3, idx);
                    }

                    
                    Dispatcher.Invoke(() =>
                    {
                        konzolaGaraza.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " +
                            "Klijent " + clientEndPoint + " se povezao na port: " + port +
                            " (" + povezani[portIndex] + "/2)\n");
                        konzolaGaraza.ScrollToEnd();
                    });

                    // Uzvrati poruku klijentu
                    var poruka = Encoding.UTF8.GetBytes($"Uspešno ste se povezali sa garazom! ({povezani[portIndex]}/2)\n");
                    try
                    {
                        await clientSocket.SendAsync(new ArraySegment<byte>(poruka), SocketFlags.None);
                    }
                    catch (Exception ex)
                    {
                        Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri slanju poruke: {ex.Message}");
                    }

                    _ = Task.Run(() => MonitorujKlijenta(clientSocket, portIndex));

                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri prihvatanju klijenta na portu {port}: {ex.Message}");
                }
            }
        }

        private void MonitorujKlijenta(Socket clientSocket, int portIndex)
        {
            byte[] buffer = new byte[1];

            while (clientSocket != null && clientSocket.Connected)
            {
                try
                {
                    // Pokušaj da pročitaš jedan bajt - ako vrati 0, klijent je otkačen
                    int bytesReceived = clientSocket.Receive(buffer, SocketFlags.Peek);

                    if (bytesReceived == 0)
                    {
                        // ✅ Klijent je otkačen!
                        break;
                    }

                    Thread.Sleep(100);  // Čekaj pre nego što ponovi
                }
                catch (SocketException)
                {
                    // ✅ Greška = klijent je otkačen
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }

            // ✅ Klijent je otkačen - smanji brojač i disable dugme
            lock (_lock)
            {
                povezani[portIndex]--;

                Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] " +
                    "Klijent sa porta " + (50000 + portIndex) + " se otkačio (" + povezani[portIndex] + "/2)");

                // Ukloni iz liste
                var timLista = GetTimZaPort(portIndex);
                if (timLista != null)
                {
                    timLista.RemoveAll(t => t.ep?.Equals(clientSocket.RemoteEndPoint) ?? false);
                }

                // ✅ Ako nema više klijenta, disable dugme
                if (povezani[portIndex] == 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        DisableAutoButtonForTeam(portIndex);
                    });
                }

                _clients.Remove(clientSocket);
            }

            // Zatvori socket
            try { clientSocket.Shutdown(SocketShutdown.Both); } catch { }
            try { clientSocket.Close(); } catch { }
        }

        private List<Trkaci> GetTimZaPort(int portIndex)
        {
            return portIndex switch
            {
                0 => Honda,      // port 50000
                1 => Mercedes,   // port 50001
                2 => Ferari,     // port 50002
                3 => Reno,       // port 50003
            };
        }
        private void Ispisi(string poruka)
        {
            Dispatcher.Invoke(() =>
            {
                konzolaGaraza.AppendText(poruka + "\n");
                konzolaGaraza.ScrollToEnd();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _cts?.Cancel();
            
            lock (_lock)
            {
                foreach (var listener in _listeners)
                {
                    listener?.Close();
                }
                _listeners.Clear();

                foreach (var client in _clients)
                {
                    client?.Close();
                }
                _clients.Clear();
                if(udpSocket != null)
                {
                    udpSocket.Close();
                }
            }
        }

        private void PrikaziUnosParametaraTrka()
        {
            GarazaUnosParametaraTrke gu = new GarazaUnosParametaraTrke();

            if(gu.ShowDialog() == true)
            {
                DuzinaStaze = gu.DuzinaStaze;
                OsnovnoVremeKruga = gu.OsnovnoVremeKruga;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void OtvoriUdpSoket(CancellationToken cancellationToken)
        {
            try
            {

                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                udpSocket.Bind(new IPEndPoint(IPAddress.Any, 50007));
                udpSocket.Blocking = false;
                Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] UDP soket otvoren na portu 50007");

                _ = Task.Run(() => ReceiveUdpLoop(cancellationToken));
            }
            catch (Exception ex)
            {
                Ispisi($"Server greška: {ex.Message}");
            }
        }

        private async Task ReceiveUdpLoop(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested && udpSocket != null)
            {
                try
                {
                    // Čitaj UDP poruke
                    int bytesReceived = udpSocket.ReceiveFrom(buffer, ref remoteEP);

                    if (bytesReceived > 0)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                        // Ispis u UI
                        Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] " +
                            "[UDP 50007] Primljena od " + remoteEP + ": " + receivedMessage);

                        // Odgovori klijentu
                        
                        
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    // Nema dostupnih podataka
                    Thread.Sleep(10);
                    continue;
                }
                catch (Exception ex)
                {
                    Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri čitanju UDP podataka: {ex.Message}");
                    break;
                }
            }
        }

        // Pomoćna funkcija — enable auto dugme za tim (sa idx parametrom)
        private void EnableAutoButtonForTeam(int portIndex, int idx)
        {
            string teamName = GetTeamName(portIndex);
            var (autoButton1, autoButton2) = FindAutoButtons(teamName, idx);
            
            if (autoButton1 != null)
            {
                autoButton1.IsEnabled = true;
                autoButton1.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            if (autoButton2 != null)
            {
                autoButton2.IsEnabled = true;
                autoButton2.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
        }

        // Pomoćna funkcija — disable auto dugme za tim (bez idx parametra)
        private void DisableAutoButtonForTeam(int portIndex)
        {
            string teamName = GetTeamName(portIndex);
            Ispisi($"[AUTO] Onemogućen auto dugme za tim: {teamName}");

            var (autoButton1, autoButton2) = FindAutoButtonsAll(teamName);
            
            if (autoButton1 != null)
            {
                autoButton1.IsEnabled = false;
                autoButton1.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
            if (autoButton2 != null)
            {
                autoButton2.IsEnabled = false;
                autoButton2.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        // Pomoćna funkcija — pronađi auto dugme po imenu tima i idx-u
        private (Button, Button) FindAutoButtons(string teamName, int idx)
        {
            var btn1 = (Button)this.FindName($"btnPosalji{teamName}{idx}");
            var btn2 = (Button)this.FindName($"btnKontrolisi{teamName}{idx}");
            return (btn1, btn2);
        }

        // Pomoćna funkcija — pronađi sve auto dugme po imenu tima (za disable)
        private (Button, Button) FindAutoButtonsAll(string teamName)
        {
            // Ako trebaš sve dugme za taj tim, možeš koristiti idx 1 kao default
            // ili pronaći sve dinamički
            var btn1 = (Button)this.FindName($"btnPosalji{teamName}1");
            var btn2 = (Button)this.FindName($"btnKontrolisi{teamName}1");
            return (btn1, btn2);
        }

        // Pomoćna funkcija — vrati ime tima na osnovu porta
        private string GetTeamName(int portIndex)
        {
            return portIndex switch
            {
                0 => "Honda",
                1 => "Mercedes",
                2 => "Ferari",
                3 => "Reno",
                _ => "Unknown"
            };
        }
    }
}