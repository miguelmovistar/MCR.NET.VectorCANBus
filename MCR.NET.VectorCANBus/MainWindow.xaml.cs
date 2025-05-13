using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using vxlapi_NET;

namespace MCR.NET.VectorCANBus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Vector
        private static XLDriver CANDemo = new XLDriver();
        private static XLClass.xl_driver_config driverConfig = new XLClass.xl_driver_config();
        private static int portHandle = 0;
        private static UInt64 accessMask = 0;
        private static UInt64 permissionMask = 0;
        private static UInt64 txMask = 0;
        private static UInt64 rxMask = 0;
        private static int txCi = -1;
        private static int rxCi = -1;
        private static EventWaitHandle xlEvWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, null);
        private static Thread rxThread;
        private static uint hwIndex = 0;
        private static uint hwChannel = 0;
        private static XLDefine.XL_HardwareType hwType = XLDefine.XL_HardwareType.XL_HWTYPE_NONE;

        // Variables del método RXThread
        static ConcurrentQueue<XLClass.xl_event> can_xl_event_queue = new ConcurrentQueue<XLClass.xl_event>();
        private static bool overrun_flag = false;
        private static int port_handle;
        private static int event_handle = 0;
        private static XLDefine.XL_Status status;
        private static XLClass.xl_event xl_EventCAN_receivedEvent;
        private static EventWaitHandle canWaitHandle;

        //Hilos
        Thread thread;
        bool detener = false;
        int consec = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void Inicializa() 
        {
            // Vector
            XLDefine.XL_Status status;
            status = CANDemo.XL_OpenDriver();
            status = CANDemo.XL_GetDriverConfig(ref driverConfig);
            CANDemo.XL_SetApplConfig("xlCANdemoNET", 0, XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL, 0, 0, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            CANDemo.XL_SetApplConfig("xlCANdemoNET", 1, XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL, 0, 0, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);

            while (!GetAppChannelAndTestIsOk(0, ref txMask, ref txCi) || !GetAppChannelAndTestIsOk(1, ref rxMask, ref rxCi))
            {
                //PrintAssignErrorAndPopupHwConf();
            }

            accessMask = txMask | rxMask;
            permissionMask = accessMask;
            status = CANDemo.XL_OpenPort(ref portHandle, "xlCANdemoNET", accessMask, ref permissionMask, 1024, XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);

            status = CANDemo.XL_CanRequestChipState(portHandle, accessMask);
            status = CANDemo.XL_ActivateChannel(portHandle, accessMask, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN, XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            int tempInt = -1;
            status = CANDemo.XL_SetNotification(portHandle, ref tempInt, 1);
            xlEvWaitHandle.SafeWaitHandle = new SafeWaitHandle(new IntPtr(tempInt), true);

            status = CANDemo.XL_ResetClock(portHandle);
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            detener = false;
            Inicializa();
            
            //rxThread = new Thread(new ThreadStart(RXThread));
            rxThread = new Thread(() => RXThread(lblMensaje));
            rxThread.Start();

            for (int i = 0; i < 500000; i++)
            {
                // Burst of CAN frames
                CANTransmitDemo();
            }
            //lblMensaje.Content = "ALGO...";

            // Hilos
            //detener = false;
            //thread = new Thread(MuestraFilas);
            //thread.IsBackground = true; // Ejecución del hilo en segundo plano
            //thread.Start();
        }

        public void RXThread(Label lblMensaje)
        {
            CANDemo.XL_CanSetReceiveMode(port_handle, 1, 0);
            status = CANDemo.XL_SetNotification(port_handle, ref event_handle, 1);
            if (canWaitHandle == null)
            {
                canWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "CANEventWaitHandle");
                canWaitHandle.SafeWaitHandle = new SafeWaitHandle(new IntPtr(event_handle), true);
            }

            XLDefine.XL_Status xlStatus;

            if (canWaitHandle.WaitOne(1000))
            {
                xlStatus = XLDefine.XL_Status.XL_SUCCESS;
                try
                {
                    while (xlStatus != XLDefine.XL_Status.XL_ERR_QUEUE_IS_EMPTY &
                            xlStatus != XLDefine.XL_Status.XL_ERROR &
                            xlStatus != XLDefine.XL_Status.XL_ERR_INVALID_ACCESS &
                            xlStatus != XLDefine.XL_Status.XL_ERR_INVALID_PORT &
                            overrun_flag != true &
                            detener != true)
                    {
                        xlStatus = CANDemo.XL_Receive(port_handle, ref xl_EventCAN_receivedEvent);

                        if (xlStatus == XLDefine.XL_Status.XL_SUCCESS)
                        {
                            PintaFilaGrid(CANDemo.XL_GetEventString(xl_EventCAN_receivedEvent));
                            Thread.Sleep(100);

                            if (xl_EventCAN_receivedEvent != null)
                            {
                                can_xl_event_queue.Enqueue(xl_EventCAN_receivedEvent);
                            }

                            if (xl_EventCAN_receivedEvent.tagData.can_Msg.flags == XLDefine.XL_MessageFlags.XL_CAN_MSG_FLAG_OVERRUN)
                            {
                                overrun_flag = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        public static void CANTransmitDemo()
        {
            XLDefine.XL_Status txStatus;

            // Create an event collection with 2 messages (events)
            XLClass.xl_event_collection xlEventCollection = new XLClass.xl_event_collection(2);

            // event 1
            xlEventCollection.xlEvent[0].tagData.can_Msg.id = 946;
            xlEventCollection.xlEvent[0].tagData.can_Msg.dlc = 8;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[0] = 4;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[1] = 0;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[2] = 0;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[3] = 12;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[4] = 230;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[5] = 0;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[6] = 0;
            xlEventCollection.xlEvent[0].tagData.can_Msg.data[7] = 0;
            xlEventCollection.xlEvent[0].tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG;

            // event 2
            xlEventCollection.xlEvent[1].tagData.can_Msg.id = 119;
            xlEventCollection.xlEvent[1].tagData.can_Msg.dlc = 8;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[0] = 0;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[1] = 0;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[2] = 0;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[3] = 0;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[4] = 0;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[5] = 0;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[6] = 0;
            xlEventCollection.xlEvent[1].tagData.can_Msg.data[7] = 0;
            xlEventCollection.xlEvent[1].tag = XLDefine.XL_EventTags.XL_TRANSMIT_MSG;


            // Transmit events
            txStatus = CANDemo.XL_CanTransmit(portHandle, txMask, xlEventCollection);
            //Console.WriteLine("Transmit Message      : " + txStatus);
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

        private void PintaFilaGrid(string lblMensaje)
        {
            List<customer> customers = new List<customer>();
            customer customer = new customer();
            customer.Name = "Nombre";
            customer.Edad = lblMensaje;
            customers.Add(customer);

            this.Dispatcher.Invoke(() => {
                dtgListado.ItemsSource = customers.ToList();
            });
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            detener = true;
        }

        private static bool GetAppChannelAndTestIsOk(uint appChIdx, ref UInt64 chMask, ref int chIdx)
        {
            XLDefine.XL_Status status = CANDemo.XL_GetApplConfig("xlCANdemoNET", appChIdx, ref hwType, ref hwIndex, ref hwChannel, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                Console.WriteLine("XL_GetApplConfig      : " + status);
                //PrintFunctionError();
            }

            chMask = CANDemo.XL_GetChannelMask(hwType, (int)hwIndex, (int)hwChannel);
            chIdx = CANDemo.XL_GetChannelIndex(hwType, (int)hwIndex, (int)hwChannel);
            if (chIdx < 0 || chIdx >= driverConfig.channelCount)
            {
                // the (hwType, hwIndex, hwChannel) triplet stored in the application configuration does not refer to any available channel.
                return false;
            }

            // test if CAN is available on this channel
            return (driverConfig.channel[chIdx].channelBusCapabilities & XLDefine.XL_BusCapabilities.XL_BUS_ACTIVE_CAP_CAN) != 0;
        }
    }
}