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

namespace ClientServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<Socket> _listeners = new List<Socket>();
        private readonly List<Socket> _clients = new List<Socket>();
        private readonly object _lock = new object();

        private CancellationTokenSource _cts;
        private List<Task> _serverTasks;
        
        private readonly int[] _ports = { 50000, 50001, 50002, 50003};
        private int[] povezani = { 0, 0, 0, 0 };

        public MainWindow()
        {
            InitializeComponent();
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
                IspisiGresku($"Server greška: {ex.Message}");
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
                        // Puni - odbij konekciju
                        var odbijPoruka = Encoding.UTF8.GetBytes("Nema više mesta u timu! Maksimalno 2 klijenta.\n");
                        try
                        {
                            await clientSocket.SendAsync(new ArraySegment<byte>(odbijPoruka), SocketFlags.None);
                        }
                        catch { }

                        clientSocket.Close();

                        IspisiGresku("[" + DateTime.Now.ToString("HH:mm:ss") + "] Konekcija odbijena - tim je pun!");

                        continue;  // Nastavi sa čekanjem sledećeg klijenta
                    }

                    lock (_lock)
                    {
                        _clients.Add(clientSocket);
                    }

                    var clientEndPoint = clientSocket.RemoteEndPoint;

                    // Uveći broj povezanih
                    povezani[portIndex]++;

                    // Koristi Dispatcher za ispis u UI
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
                        IspisiGresku($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri slanju poruke: {ex.Message}");
                    }

                    // Pokreni handling za ovog klijenta na posebnom tredu
                    //_ = Task.Run(() => HandleClientAsync(clientSocket, port, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    IspisiGresku($"[{DateTime.Now.ToString("HH:mm:ss")}] Greška pri prihvatanju klijenta na portu {port}: {ex.Message}");
                }
            }
        }
        private void IspisiGresku(string poruka)
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
            }
        }
    }
}