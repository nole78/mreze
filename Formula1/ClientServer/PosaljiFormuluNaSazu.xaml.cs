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
using Client.Enumeracije;

namespace ClientServer
{
    /// <summary>
    /// Interaction logic for PosaljiFormuluNaSazu.xaml
    /// </summary>
    public partial class PosaljiFormuluNaSazu : Window
    {
        public TipGume TipGuma { get; set; }
        public int KolicinaGoriva { get; set; }
        public PosaljiFormuluNaSazu()
        {
            InitializeComponent();
        }

        private void btnPosaljiFormulu_Click(object sender, RoutedEventArgs e)
        {
            if(cbTipGuma.Text.Contains("Tvrde"))
            {
                TipGuma = TipGume.Tvrde;
            }
            else if(cbTipGuma.Text.Contains("SrednjeTvrde"))
            {
                TipGuma = TipGume.SrednjeTvrde;
            }
            else
            {
                TipGuma = TipGume.Meke;
            }

            if(!Int32.TryParse(tbProcenatGoriva.Text,out int gorivo) || gorivo < 10 || gorivo > 150)
            {
                MessageBox.Show("Kolicina goriva moze biti od 10 do 150 l");
            }
            else
            {
                KolicinaGoriva = gorivo;
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}
