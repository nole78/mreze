using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Interaction logic for OdabirTima.xaml
    /// </summary>
    public partial class OdabirTima : Window
    {
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
            if (tim == Timovi.Honda)
                port = 50000;
            else if (tim == Timovi.Mercedes)
                port = 50001;
            else if (tim == Timovi.Ferari)
                port = 50002;
            else if (tim == Timovi.Reno)
                port = 50003;
        }
    }
}
