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
        bool detener = false;
        int consec = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            detener = false;
            thread = new Thread(MuestraFilas);
            thread.IsBackground = true; // Ejecución del hilo en segundo plano
            thread.Start();
        }

        private void MuestraFilas()
        {
            List<customer> customers = new List<customer>();
            customer customer = new customer();
            
            while (!detener) 
            {
                
                Thread.Sleep(50);
                customer.Name = "Nombre";
                customer.Edad = consec.ToString();

                customers.Add(customer);
                consec++;

                this.Dispatcher.Invoke(() => {
                    dtgListado.ItemsSource = customers.ToList();
                });
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            detener = true;
        }
    }
}