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



namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Socket сокет;
        private CancellationTokenSource _cts;
        private Task _rxTask;

        private int port = 0;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            izaberiTim(Timovi.Mercedes);
        }


        private void izaberiFerari_Click(object sender, RoutedEventArgs e)
        {
            izaberiTim(Timovi.Ferari);
        }

        private void izaberiReno_Click(object sender, RoutedEventArgs e)
        {
            izaberiTim(Timovi.Reno);
        }

        private void izaberiHonda_Click(object sender, RoutedEventArgs e)
        {
            izaberiTim(Timovi.Honda);
        }
        private void izaberiTim(Timovi tim)
        {
            if (tim == Timovi.Honda)
                port = 50000;
            else if (tim == Timovi.Mercedes)
                port = 50001;
            else if (tim == Timovi.Ferari)
                port = 50002;
            else if (tim == Timovi.Reno)
                port = 50003;

            сокет = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, port);
            try
            {
                сокет.Connect(serverEP);

                _cts = new CancellationTokenSource();
                _rxTask = Task.Run(() => ReceiveLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ne mogu da se povežem: " + ex.Message);
            }
        }
        private void ReceiveLoop(CancellationToken token)
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
                    Dispatcher.Invoke(() => Disconnect());
                    break;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => Disconnect());
                    break;
                }
            }
        }

        private void ObradiPoruku(string poruka)
        {
            Dispatcher.Invoke(() =>
            {
                chatBox.AppendText(poruka + "\n");
                chatBox.ScrollToEnd();
            });

            if(poruka == "Nema više mesta u timu! Maksimalno 2 klijenta.")
            {

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
                //ispisati
            }
        }
    }
}