using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Prism.Mvvm;
using Prism.Commands;
using System.Collections.Concurrent;

namespace LiftTerminal
{
    class TerminalViewModel: BindableBase
    {
        ObservableCollection<string> _traceCharacters = new ObservableCollection<string>();
        List<byte> _receivedTraceCharacters = new List<byte>();
        String _lineBreaks;
        String _selectedComPort;
        int _selectedBaudRate = SerialPortHandler.AvailableBaudRates[0];
        
        DelegateCommand _cmdOpenClose;

        DelegateCommand _cmdClearTerminal;
        
        ConcurrentQueue<byte> _rxBuffer = new ConcurrentQueue<byte>();

        SerialPortHandler _serialPortHandler;

        ManagementEventWatcher _watcher = new ManagementEventWatcher();
        WqlEventQuery _disconnectedQuery = new WqlEventQuery("__InstanceDeletionEvent",
                new TimeSpan(0, 0, 1),
                "TargetInstance ISA 'Win32_USBControllerDevice'");
        WqlEventQuery _connectedQuery = new WqlEventQuery("__InstanceCreationEvent",
                new TimeSpan(0, 0, 1),
                "TargetInstance ISA 'Win32_USBControllerDevice'");

        public TerminalViewModel()
        {
            _watcher.Query = _disconnectedQuery;
            _watcher.EventArrived += UsbDisconnectEventArrived;

            _serialPortHandler = new SerialPortHandler(_rxBuffer);
            _serialPortHandler.DataReceived += RxDataReceived;
            _traceCharacters.AddRange(new[] { "erstens", "zweitens", "drittens" });
            _cmdOpenClose = new DelegateCommand(() =>
            {
                if (_serialPortHandler.PortIsOpen)
                {
                    _serialPortHandler.StopCom();
                    _watcher.Stop();
                }
                else
                {
                    _serialPortHandler.StartCom(_selectedComPort,
                        _selectedBaudRate);
                    _watcher.Start();
                }
                RaisePropertyChanged("LabelOpenClose");
            }, ()=>!string.IsNullOrEmpty(_selectedComPort));
            _cmdClearTerminal = new DelegateCommand(() =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(
                    ()=>_traceCharacters.Clear()
                    );
            });
        }

        private void RxDataReceived(object sender, int e)
        {
            List<byte> tmpBuilder = new List<byte>();
            for (int i = 0; i < e; i++)
            {
                if (_rxBuffer.TryDequeue(out byte data))
                {
                    tmpBuilder.Add(data);
                }
            }
            _receivedTraceCharacters.AddRange(tmpBuilder);
            System.Windows.Application.Current.Dispatcher.Invoke(
                () =>
                {
                    _traceCharacters.AddRange<string>(
                        ComputeLineBrakes(tmpBuilder));
                });

        }

        public string LineBreaks
        {
            get
            {
                return _lineBreaks;
            }

            set
            {
                SetProperty<string>(ref _lineBreaks, value);
                _traceCharacters.Clear();
                _traceCharacters.AddRange(
                    ComputeLineBrakes(_receivedTraceCharacters));
            }

        }

        public string SelectedComPort
        {
            get
            {
                return _selectedComPort;
            }
            set
            {
                SetProperty<string>(ref _selectedComPort, value);
                _cmdOpenClose.RaiseCanExecuteChanged();
            }
        }

        public int SelectedBaudRate
        {
            get
            {
                return _selectedBaudRate;
            }
            set
            {
                SetProperty<int>(ref _selectedBaudRate, value);
            }
        }

        public ObservableCollection<string> TraceCharacters
        {
            get
            {
                return _traceCharacters;
            }
        }
        public string LabelOpenClose
        {
            get
            {
                if (_serialPortHandler.PortIsOpen)
                {
                    return "Close";
                }
                return "Open";
            }
        }

        public ICommand CmdOpenClose
        {
            get
            {
                return _cmdOpenClose;
            }
        }

        public ICommand CmdClearTerminal
        {
            get
            {
                return _cmdClearTerminal;
            }
        }

        public int[] AvailableBaudRates
        {
            get
            {
                return SerialPortHandler.AvailableBaudRates;
            }
        }

        public string[] AvailablePorts
        {
            get
            {
                return SerialPortHandler.AvailablePorts;
            }
        }

        IEnumerable<string> ComputeLineBrakes(List<Byte> byteList)
        {
            List<string> formattedTraceLines = new List<string>();
            var breakBytes = new string[0];
            if (!string.IsNullOrEmpty(LineBreaks))
            {
                breakBytes = LineBreaks.Split(' ', ',', ';');
            }
            SortedSet<byte> breaks = new SortedSet<byte>();
            foreach( var str in breakBytes)
            {
                if( byte.TryParse(str, NumberStyles.AllowHexSpecifier, null, out byte b))
                {
                    breaks.Add(b);
                }
            }
            StringBuilder tmpBuilder = new StringBuilder();
            foreach( var traceByte in byteList)
            {
                if( breaks.Contains( traceByte))
                {
                    if(tmpBuilder.Length > 0)
                    {
                        formattedTraceLines.Add(tmpBuilder.ToString());
                        tmpBuilder.Clear();
                    }
                }
                tmpBuilder.AppendFormat("{0:X02} ", traceByte);
            }
            if (tmpBuilder.Length > 0)
            {
                formattedTraceLines.Add(tmpBuilder.ToString());
                tmpBuilder.Clear();
            }
            return formattedTraceLines;
        }

        internal void SendButtonDown(ButtonType buttonType, int floor)
        {
            int upperNibble = ((int)buttonType | 1) << 4;
            
            //TraceCharacters.Add(string.Format("Button down {0} {1}",buttonType, floor));
            byte[] data = new byte[] { (byte)((upperNibble) |floor) };
            _serialPortHandler.Write(data, 0, data.Length);
        }

        internal void SendButtonUp(ButtonType buttonType, int floor)
        {
            //TraceCharacters.Add(string.Format("Button up {0} {1}", buttonType, floor));
            int upperNibble = ((int)buttonType) << 4;
            byte[] data = new byte[] { (byte)((upperNibble) | floor) };
            _serialPortHandler.Write(data, 0, data.Length);
        }

        void DetectUsbDisconnect()
        {

            _watcher.Start();
        }

        private void UsbDisconnectEventArrived(object sender, EventArrivedEventArgs e)
        {
            RaisePropertyChanged("AvailablePorts");
            if ( !SerialPortHandler.AvailablePorts.Contains(_selectedComPort)) // the selected port is not in the  list anymore
            {
                _serialPortHandler.StopCom();
                _selectedComPort = "";
                RaisePropertyChanged("LabelOpenClose");
                _watcher.Stop();
                _watcher.EventArrived -= UsbDisconnectEventArrived;
                _watcher.Query = _connectedQuery;
                _watcher.EventArrived += _watcher_EventArrived;
                _watcher.Start();
            }

        }

        private void _watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            RaisePropertyChanged("LabelOpenClose");
            RaisePropertyChanged("AvailablePorts");
            _watcher.Stop();
            _watcher.Query = _disconnectedQuery;
            _watcher.EventArrived += UsbDisconnectEventArrived;
            _watcher.EventArrived -= _watcher_EventArrived;
        }
    }
}
