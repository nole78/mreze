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

namespace ClientServer
{
    /// <summary>
    /// Interaction logic for KontrolaFormule.xaml
    /// </summary>
    public partial class KontrolaFormule : Window
    {
        public string poruka = "";
        public KontrolaFormule()
        {
            InitializeComponent();
        }

        private void btnVoziSporije_Click(object sender, RoutedEventArgs e)
        {
            poruka = "Vozi sporije";

            this.DialogResult = true;
            this.Close();
        }

        private void btnVoziSrednje_Click(object sender, RoutedEventArgs e)
        {
            poruka = "Vozi srednjim tempom";

            this.DialogResult = true;
            this.Close();
        }

        private void btnVoziBrze_Click(object sender, RoutedEventArgs e)
        {
            poruka = "Vozi brze";

            this.DialogResult = true;
            this.Close();
        }

        private void btnVratiSeUGarazu_Click(object sender, RoutedEventArgs e)
        {
            poruka = "Sidji sa staze";

            this.DialogResult = true;
            this.Close();
        }
    }
}
