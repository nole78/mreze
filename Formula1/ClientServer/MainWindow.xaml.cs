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
using System.Windows.Shell;
using Client.Enumeracije;
using Client.Modeli;
namespace ClientServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        struct Trkaci
        {
            public int brVozaca;
            public Timovi tim;
            public EndPoint ep;
            public int udpPort;
        }
        private List<Trkaci> Honda = new List<Trkaci>();
        private List<Trkaci> Mercedes = new List<Trkaci>();
        private List<Trkaci> Ferari = new List<Trkaci>();
        private List<Trkaci> Reno = new List<Trkaci>();
        
        private readonly List<Socket> _clients = new List<Socket>();
        private readonly Dictionary<int, Socket> _udpSockets = new Dictionary<int, Socket>();  // ✅ UDP soketi po portu


        private readonly object _lock = new object();

        private CancellationTokenSource _cts;
        private List<Task> _serverTasks;
        private Socket tcpSoket;
        private Task _udpTask;

        private readonly int _tcpPort = 50000;
        private readonly int[] _udpPorts = { 50004, 50005, 50006, 50007, 50008, 50009, 50010, 50011 };
        private int brojUdpPovezanih = 0;
        private int[] povezani = { 0, 0, 0, 0 };

        private int DuzinaStaze = 0;
        private double OsnovnoVremeKruga = 0;

        private volatile bool _isClosing = false;
        public MainWindow()
        {
            InitializeComponent();
            PrikaziUnosParametaraTrka();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartServerTcp();
        }

        private async void StartServerTcp()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _serverTasks = new List<Task>();


                tcpSoket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    
                    
                Dispatcher.Invoke(() =>
                {
                    konzolaGaraza.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + $"Server START na portu {_tcpPort}\n");
                    konzolaGaraza.ScrollToEnd();
                });

                tcpSoket.Bind(new IPEndPoint(IPAddress.Any, _tcpPort));
                tcpSoket.Listen(100);
                    
                    
                var task = AcceptClientsTcp(tcpSoket, _tcpPort, _cts.Token);
                _serverTasks.Add(task);
                

                await Task.WhenAll(_serverTasks);
            }
            catch (Exception ex)
            {
                Ispisi($"Server greška: {ex.Message}");
            }
        }
        private async Task AcceptClientsTcp(Socket listener, int port, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Socket clientSocket = await listener.AcceptAsync();
                    var clientEndPoint = clientSocket.RemoteEndPoint;

                    lock (_lock)
                    {
                        _clients.Add(clientSocket);
                    }

                    // ✅ Čekaj da klijent pošalje broj tima koji trebа
                    _ = Task.Run(() => DobroDosoTcp(clientSocket, clientEndPoint));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri prihvatanju klijenta: {ex.Message}");
                }
            }
        }
        private async Task DobroDosoTcp(Socket clientSocket, EndPoint clientEndPoint)
        {
            int portIndex = -1;
            int udpPort = -1;

            try
            {
                byte[] buffer = new byte[10];
                int bytesReceived = await clientSocket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                if (bytesReceived > 0)
                {
                    string tim = Encoding.UTF8.GetString(buffer, 0, bytesReceived).Trim().ToLower();
                    Ispisi(tim);
                    
                    if (tim.Length != 0)
                    {
                        int timPort = tim switch
                        {
                            "honda" => 50000,
                            "mercedes" => 50001,
                            "ferari" => 50002,
                            "reno" => 50003,
                            _ => -1
                        };

                        if (timPort == -1)
                        {
                            var greska = Encoding.UTF8.GetBytes("Neispravan broj tima!");
                            await clientSocket.SendAsync(new ArraySegment<byte>(greska), SocketFlags.None);
                            clientSocket.Close();
                            lock (_lock) { _clients.Remove(clientSocket); }
                            return;
                        }

                        portIndex = timPort - 50000;

                        // ✅ Prebaci sve ispod lock-a IZVAN lock bloka
                        int tempUdpPort = -1;
                        bool canConnect = false;

                        lock (_lock)
                        {
                            if (povezani[portIndex] >= 2)
                            {
                                canConnect = false;
                                return;
                            }

                            if (brojUdpPovezanih < _udpPorts.Length)
                            {
                                tempUdpPort = _udpPorts[brojUdpPovezanih];
                                brojUdpPovezanih++;
                                canConnect = true;
                            }
                            else
                            {
                                canConnect = false;
                                return;
                            }
                        }

                        // ✅ Ako je konekcija odbijena, pošalji grešku IZVAN lock-a
                        if (!canConnect)
                        {
                            var odbijPoruka = Encoding.UTF8.GetBytes("Nema više mesta u timu");
                            try
                            {
                                await clientSocket.SendAsync(new ArraySegment<byte>(odbijPoruka), SocketFlags.None);
                            }
                            catch { }
                            
                            clientSocket.Close();
                            lock (_lock) { _clients.Remove(clientSocket); }
                            Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] Konekcija odbijena - tim je pun!");
                            return;
                        }

                        udpPort = tempUdpPort;

                        // ✅ Kreni sa slušanjem na UDP portu - IZVAN lock-a
                        OtvoriUdpSoketZaKlijenta(udpPort);

                        // ✅ Dodaj trkača IZVAN lock-a
                        lock (_lock)
                        {
                            povezani[portIndex]++;

                            Trkaci trkaci = new Trkaci
                            {
                                brVozaca = GetTimZaPort(portIndex).Count,
                                tim = GetTimEnum(portIndex),
                                ep = clientEndPoint,
                                udpPort = udpPort
                            };

                            GetTimZaPort(portIndex).Add(trkaci);

                            // ✅ Sve UI operacije u Dispatcher.Invoke
                            Dispatcher.Invoke(() =>
                            {
                                konzolaGaraza.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " +
                                    "Klijent " + clientEndPoint + " se povezao na tim: " + GetTeamName(portIndex) +
                                    " (UDP port: " + udpPort + ") (" + povezani[portIndex] + "/2)\n");
                                konzolaGaraza.ScrollToEnd();
                                
                                EnableAutoButtonForTeam(portIndex, trkaci.brVozaca + 1);
                            });
                        }

                        // ✅ Slanje potvrde - IZVAN lock-a
                        string potvrdaPoruka = $"Port: {udpPort}";
                        var poruka = Encoding.UTF8.GetBytes(potvrdaPoruka);
                        try
                        {
                            await clientSocket.SendAsync(new ArraySegment<byte>(poruka), SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            Ispisi($"Greška pri slanju potvrde: {ex.Message}");
                        }

                        _ = Task.Run(() => MonitorujKlijenta(clientSocket, portIndex, udpPort));
                    }
                    else
                    {
                        var greska = Encoding.UTF8.GetBytes("Neispravan broj tima!");
                        try
                        {
                            await clientSocket.SendAsync(new ArraySegment<byte>(greska), SocketFlags.None);
                        }
                        catch { }
                        
                        clientSocket.Close();
                        lock (_lock) { _clients.Remove(clientSocket); }
                    }
                }
                else
                {
                    clientSocket.Close();
                    lock (_lock) { _clients.Remove(clientSocket); }
                }
            }
            catch (OperationCanceledException)
            {
                clientSocket.Close();
                lock (_lock) { _clients.Remove(clientSocket); }
                Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Klijent je otkačen pre nego što je poslao broj tima");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                clientSocket.Close();
                lock (_lock) { _clients.Remove(clientSocket); }
                Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Klijent je prekinuo konekciju");
            }
            catch (Exception ex)
            {
                clientSocket.Close();
                lock (_lock) { _clients.Remove(clientSocket); }
                Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri obradi klijenta: {ex.Message}");
            }
        }
        private void MonitorujKlijenta(Socket clientSocket, int portIndex, int udpPort) 
        {
            byte[] buffer = new byte[1];
            EndPoint clientEndPoint = clientSocket.RemoteEndPoint;

            while (clientSocket != null && clientSocket.Connected)
            {
                try
                {
                    int bytesReceived = clientSocket.Receive(buffer, SocketFlags.Peek);

                    if (bytesReceived == 0)
                    {
                        break;
                    }

                    Thread.Sleep(100);
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }

            lock (_lock)
            {
                povezani[portIndex]--;
                brojUdpPovezanih--;
                Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] " +
                    "Klijent sa tima " + GetTeamName(portIndex) + " se otkačio (" + povezani[portIndex] + "/2)");

                var timLista = GetTimZaPort(portIndex);
                if (timLista != null && clientEndPoint != null)
                {
                    var trkacZaBrisanje = timLista.FirstOrDefault(t => t.ep?.Equals(clientEndPoint) ?? false);

                    if (trkacZaBrisanje.ep != null)
                    {
                        int brVozaca = trkacZaBrisanje.brVozaca;

                        Ispisi($"[AUTO] Uklonjen vozač br. {brVozaca} iz tima {trkacZaBrisanje.tim} (UDP port: {udpPort})");

                        timLista.RemoveAll(t => t.ep?.Equals(clientEndPoint) ?? false);

                        // ✅ Zatvori UDP soket ako nema više klijenta na tom portu
                        if (_udpSockets.ContainsKey(udpPort))
                        {
                            try
                            {
                                _udpSockets[udpPort].Close();
                                _udpSockets.Remove(udpPort);
                            }
                            catch { }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            DisableAutoButtonForTeam(portIndex, brVozaca + 1);
                        });
                    }
                }

                _clients.Remove(clientSocket);
            }

            try { clientSocket.Shutdown(SocketShutdown.Both); } catch { }
            try { clientSocket.Close(); } catch { }
        }
        private void OtvoriUdpSoketZaKlijenta(int udpPort)
        {
            try
            {
                if (_udpSockets.ContainsKey(udpPort))
                {
                    return;  // ✅ Soket već postoji
                }

                var udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSocket.Bind(new IPEndPoint(IPAddress.Any, udpPort));
                udpSocket.Blocking = false;

                _udpSockets[udpPort] = udpSocket;

                // ✅ Kreni sa slušanjem na ovom UDP portu
                _ = Task.Run(() => ReceiveUdpLoop(udpPort, _cts.Token));

                Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] UDP soket otvoren na portu " + udpPort);
            }
            catch (Exception ex)
            {
                Ispisi($"Greška pri otvaranju UDP soketa: {ex.Message}");
            }
        }
        private async Task ReceiveUdpLoop(int udpPort, CancellationToken cancellationToken)
        {
            if (!_udpSockets.ContainsKey(udpPort))
                return;

            var udpSocket = _udpSockets[udpPort];
            byte[] buffer = new byte[4096];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested && _udpSockets.ContainsKey(udpPort))
            {
                try
                {
                    int bytesReceived = udpSocket.ReceiveFrom(buffer, ref remoteEP);

                    if (bytesReceived > 0)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                        Ispisi("[" + DateTime.Now.ToString("HH:mm:ss") + "] " +
                            "[UDP " + udpPort + "] Primljena od " + remoteEP + ": " + receivedMessage);
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    Thread.Sleep(10);
                    continue;
                }
                catch (Exception ex)
                {
                    if (!_isClosing)
                    {
                        Ispisi($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri čitanju UDP {udpPort}: {ex.Message}");
                    }
                    break;
                }
            }
        }
        private void posaljiUdpPoruku(EndPoint ep, string poruka)
        {
            try
            {
                // ✅ Pronađi Trkaci po EndPoint-u
                Trkaci? trkacZaSlanje = null;

                foreach (var lista in new[] { Honda, Mercedes, Ferari, Reno })
                {
                    var pronaden = lista.FirstOrDefault(t => t.ep?.Equals(ep) ?? false);
                    if (pronaden.ep != null)
                    {
                        trkacZaSlanje = pronaden;
                        break;
                    }
                }

                if (trkacZaSlanje == null)
                {
                    Ispisi("Vozač nije pronađen!");
                    return;
                }

                int udpPort = trkacZaSlanje.Value.udpPort;

                if (!_udpSockets.ContainsKey(udpPort))
                {
                    Ispisi("UDP soket nije inicijalizovan za port " + udpPort);
                    return;
                }

                var udpSocket = _udpSockets[udpPort];
                var ipEndPoint = ep as IPEndPoint;

                if (ipEndPoint != null)
                {
                    IPEndPoint clientUdpEndPoint = new IPEndPoint(ipEndPoint.Address, udpPort);
                    byte[] binarnaPoruka = Encoding.UTF8.GetBytes(poruka);
                    int sent = udpSocket.SendTo(binarnaPoruka, clientUdpEndPoint);

                    Ispisi($"[UDP SLANJE] Poslato {sent} bajtova na {clientUdpEndPoint}: {poruka}");
                }
                else
                {
                    Ispisi("Neispravan endpoint format!");
                }
            }
            catch (Exception ex)
            {
                Ispisi($"Greška pri slanju UDP poruke: {ex.Message}");
            }
        }
        private void Ispisi(string poruka)
        {
            if (_isClosing)
                return;

            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (!_isClosing && konzolaGaraza != null)
                    {
                        konzolaGaraza.AppendText(poruka + "\n");
                        konzolaGaraza.ScrollToEnd();
                    }
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch { }
        }
        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            
            base.OnClosed(e);
            _cts?.Cancel();
            
            lock (_lock)
            {
                if (tcpSoket != null)
                {
                    tcpSoket.Close();
                }


                foreach (var client in _clients)
                {
                    client?.Close();
                }
                _clients.Clear();
                foreach (var udpSocket in _udpSockets.Values)
                {
                    try
                    {
                        udpSocket?.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    finally
                    {
                        udpSocket?.Close();
                        udpSocket?.Dispose();
                    }
                }
                _udpSockets.Clear();
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
        private void DisableAutoButtonForTeam(int portIndex, int idx)
        {
            string teamName = GetTeamName(portIndex);

            var (autoButton1, autoButton2) = FindAutoButtons(teamName, idx);
            
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
        private (Button, Button) FindAutoButtons(string teamName, int idx)
        {
            var btn1 = (Button)this.FindName($"btnPosalji{teamName}{idx}");
            var btn2 = (Button)this.FindName($"btnKontrolisi{teamName}{idx}");
            return (btn1, btn2);
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
        private string GetTeamName(int portIndex)
        {
            return portIndex switch
            {
                0 => "Honda",
                1 => "Mercedes",
                2 => "Ferari",
                3 => "Reno",
            };
        }
        private Timovi GetTimEnum(int portIndex)
        {
            return portIndex switch
            {
                0 => Timovi.Honda,
                1 => Timovi.Mercedes,
                2 => Timovi.Ferari,
                3 => Timovi.Reno,
                _ => Timovi.Honda
            };
        }
        private void btnPosaljiMercedes1_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Mercedes[0].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Mercedes[0].ep, poruka);
            }
        }

        private void btnPosaljiFerari1_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Ferari[0].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Ferari[0].ep, poruka);
            }
        }

        private void btnPosaljiReno1_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Reno[0].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Reno[0].ep, poruka);
            }
        }

        private void btnPosaljiHonda1_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Honda[0].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Honda[0].ep, poruka);
            }
        }

        private void btnPosaljiMercedes2_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Mercedes[1].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Mercedes[1].ep, poruka);
            }
        }

        private void btnPosaljiFerari2_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Ferari[1].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Ferari[1].ep, poruka);
            }
        }

        private void btnPosaljiReno2_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Reno[1].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Reno[1].ep, poruka);
            }
        }

        private void btnPosaljiHonda2_Click(object sender, RoutedEventArgs e)
        {
            PosaljiFormuluNaSazu pf = new PosaljiFormuluNaSazu();
            if (pf.ShowDialog() == true)
            {
                int procenatGoriva = pf.KolicinaGoriva;
                char izabranTipGuma = pf.TipGuma switch
                {
                    TipGume.SrednjeTvrde => 'M',
                    TipGume.Tvrde => 'H',
                    TipGume.Meke => 'S'
                };
                string porukaSpec = $"specifikacije kruga: {DuzinaStaze} {OsnovnoVremeKruga}";
                posaljiUdpPoruku(Honda[1].ep, porukaSpec);
                string poruka = $"Izlazak na stazu: {izabranTipGuma},{procenatGoriva}";
                posaljiUdpPoruku(Honda[1].ep, poruka);
            }
        }

        private void btnKontrolisiMercedes1_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if (poruka.Length > 0)
                    posaljiUdpPoruku(Mercedes[0].ep, poruka);
            }
        }

        private void btnKontrolisiFerari1_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if (poruka.Length > 0)
                    posaljiUdpPoruku(Ferari[0].ep, poruka);
            }
        }

        private void btnKontrolisiReno1_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if (poruka.Length > 0)
                    posaljiUdpPoruku(Reno[0].ep, poruka);
            }
        }

        private void btnKontrolisiHonda1_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if(poruka.Length > 0)
                    posaljiUdpPoruku(Honda[0].ep, poruka);
            }
        }

        private void btnKontrolisiMercedes2_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if (poruka.Length > 0)
                    posaljiUdpPoruku(Mercedes[1].ep, poruka);
            }
        }

        private void btnKontrolisiFerari2_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if (poruka.Length > 0)
                    posaljiUdpPoruku(Ferari[1].ep, poruka);
            }
        }

        private void btnKontrolisiReno2_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if (poruka.Length > 0)
                    posaljiUdpPoruku(Reno[1].ep, poruka);
            }
        }

        private void btnKontrolisiHonda2_Click(object sender, RoutedEventArgs e)
        {
            KontrolaFormule kf = new KontrolaFormule();
            if (kf.ShowDialog() == true)
            {
                string poruka = kf.poruka;
                if (poruka.Length > 0)
                    posaljiUdpPoruku(Honda[1].ep, poruka);
            }
        }
    }
}