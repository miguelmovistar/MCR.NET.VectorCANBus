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

namespace MCR.NET.VectorCANBus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Thread thread;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            thread = new Thread(MuestraFilas);
            thread.IsBackground = true;
            thread.Start();
        }

        private void MuestraFilas()
        {
            List<customer> customers = new List<customer>();
            customer customer = new customer();

            for (int i = 0; i < 500000; i++) 
            {
                Thread.Sleep(1000);
                customer.Name = "Nombre";
                customer.Edad = i.ToString();

                customers.Add(customer);

                this.Dispatcher.Invoke(() => { 
                    dtgListado.ItemsSource = customers.ToList();
                });
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            thread.Abort();
        }
    }
}