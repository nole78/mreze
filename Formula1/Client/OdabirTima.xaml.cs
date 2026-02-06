using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Client.Enumeracije;
using Client.Modeli;
namespace Client
{
    /// <summary>
    /// Interaction logic for OdabirTima.xaml
    /// </summary>
    public partial class OdabirTima : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public Timovi izabraniTim { get; set; }
        public OdabirTima()
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
            int port = -1;

            if (tim == Timovi.Honda)
                port = 50000;
            else if (tim == Timovi.Mercedes)
                port = 50001;
            else if (tim == Timovi.Ferari)
                port = 50002;
            else if (tim == Timovi.Reno)
                port = 50003;

            if (port != -1)
            {
                izabraniTim = tim;
                // Zatvori prozor i vrati rezultat
                this.DialogResult = true;
                this.Close();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }
    }
}
