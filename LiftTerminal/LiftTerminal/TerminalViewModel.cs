using System;
using System.Reflection;
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
using Microsoft.Win32;
using RuntimeCheck;

namespace LiftTerminal
{
    class TerminalViewModel: BindableBase
    {
        const string ReprAscii = "ASCII";
        const string ReprHex = "Hexadecimal";

        delegate IEnumerable<string> ComputeTraceLineDelegate(List<Byte> byteList);

        ObservableCollection<string> _traceCharacters = new ObservableCollection<string>();
        Dictionary<string, ComputeTraceLineDelegate> _traceLineOptions; 
        List<byte> _receivedTraceCharacters = new List<byte>();
        List<byte> _statusBytes = new List<byte>();
        string _lineBreaks;
        static string _reminder = ""; 
        string _selectedComPort;
        string _avrInput = "";
        int _selectedBaudRate = SerialPortHandler.AvailableBaudRates[0];
        int _liftStatus;
        ComputeTraceLineDelegate _computeTraceLines;
        string _selectedTraceRepresentation = ReprHex;
        DelegateCommand _cmdOpenClose;
        //DelegateCommand _cmdOpenTestLib;
        DelegateCommand _cmdWriteInputToAvr;
        DelegateCommand _cmdClearTerminal;
        
        ConcurrentQueue<byte> _rxBuffer = new ConcurrentQueue<byte>();

        SerialPortHandler _serialPortHandler;
        LiftStatusReceiver _rxLiftStatus = new LiftStatusReceiver();

        ManagementEventWatcher _watcher = new ManagementEventWatcher();
        WqlEventQuery _disconnectedQuery = new WqlEventQuery("__InstanceDeletionEvent",
                new TimeSpan(0, 0, 1),
                "TargetInstance ISA 'Win32_USBControllerDevice'");
        WqlEventQuery _connectedQuery = new WqlEventQuery("__InstanceCreationEvent",
                new TimeSpan(0, 0, 1),
                "TargetInstance ISA 'Win32_USBControllerDevice'");

        public TerminalViewModel()
        {
            
            _traceLineOptions = new Dictionary<string, ComputeTraceLineDelegate>()
            {
                {ReprAscii, ComputeLineBrakesAsciiContent  },
                {ReprHex, ComputeLineBrakesHexContent }
            };
            _watcher.Query = _disconnectedQuery;
            _watcher.EventArrived += UsbDisconnectEventArrived;
            _computeTraceLines = ComputeLineBrakesHexContent;
            _serialPortHandler = new SerialPortHandler(_rxBuffer);
            _serialPortHandler.DataReceived += RxDataReceived;
            _rxLiftStatus.StatusReceived += LiftStatusReceived;

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
                _cmdWriteInputToAvr.RaiseCanExecuteChanged();
            }, ()=>!string.IsNullOrEmpty(_selectedComPort));
            _cmdClearTerminal = new DelegateCommand(() =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(
                    ()=>_traceCharacters.Clear()
                    );
            });
            _cmdWriteInputToAvr = new DelegateCommand(()=>
                {
                    if (ToByteArray(_avrInput, out byte[] bytes))
                    {
                        var packet = PackAvrMessage(bytes, AvrPacketType.TestCommand);
                        _serialPortHandler.Write(packet, 0, packet.Length);
                    }
                    else
                    {
                        throw new InvalidCastException(
                            string.Format("{0} is not a valid HEX string", _avrInput));
                    }
                }, () => _serialPortHandler.PortIsOpen);

            
        }

        public bool IsEnabledL1
        {
            get => (this._liftStatus & 1) != 1 && _liftStatus != 0;
        }

        public bool IsEnabledL2
        {
            get => (this._liftStatus & 2) != 2 && _liftStatus != 0;
        }

        public bool IsEnabledL3
        {
            get => (this._liftStatus & 4) != 4 && _liftStatus != 0;
        }

        public bool IsEnabledL4
        {
            get => (this._liftStatus & 8) != 8 && _liftStatus != 0;
        }

        public string[] TraceOptions
        {
            get
            {
                return _traceLineOptions.Keys.ToArray();
            }
        }

        public string TraceRepresentation
        {
            get
            {
                return _selectedTraceRepresentation;
            }
            set
            {
                SetProperty<string>(ref _selectedTraceRepresentation, value);
                _computeTraceLines = _traceLineOptions[_selectedTraceRepresentation];
            }
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
                    _computeTraceLines(_receivedTraceCharacters));
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

        public string AvrInput
        {
            get
            {
                return _avrInput;
            }
            set
            {
                SetProperty<string>(ref _avrInput, value);
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

        //public ICommand CmdTestLibOpen
        //{
        //    get
        //    {
        //        return _cmdOpenTestLib;
        //    }
        //}

        public ICommand CmdWriteHexString
        {
            get
            {
                return _cmdWriteInputToAvr;
            }
        }

        static bool ToByteArray(string hexString, out byte[] byteArray )
        {
            var byteRepr = hexString.Split(',', ' ', ';');
            List<byte> bytes = new List<byte>();
            foreach( var s in byteRepr)
            {
                if (!string.IsNullOrEmpty(s.Trim()))
                {
                    if (!byte.TryParse(s.Trim(), NumberStyles.HexNumber, null, out byte result))
                    {
                        byteArray = bytes.ToArray();
                        return false;
                    }
                    bytes.Add(result);
                }
            }
            byteArray = bytes.ToArray();
            return true;
        }

        static IEnumerable<string> ComputeLineBrakesAsciiContent(List<Byte> byteList)
        {
            List<string> formattedTraceLines = new List<string>();
            var str = _reminder + ASCIIEncoding.ASCII.GetString( byteList.ToArray() );
            var lines = str.Split('\n');
            formattedTraceLines.AddRange(  lines.Take(lines.Count()-1) );
            _reminder = lines.Last();
            return formattedTraceLines;
        }

        IEnumerable<string> ComputeLineBrakesHexContent(List<Byte> byteList)
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
            var packet = PackAvrMessage(data, AvrPacketType.LiftSimulatorButton);
            _serialPortHandler.Write(packet, 0, packet.Length);
        }

        internal void SendButtonUp(ButtonType buttonType, int floor)
        {
            //TraceCharacters.Add(string.Format("Button up {0} {1}", buttonType, floor));
            int upperNibble = ((int)buttonType) << 4;
            byte[] data = new byte[] { (byte)((upperNibble) | floor) };
            var packet = PackAvrMessage(data, AvrPacketType.LiftSimulatorButton);
            _serialPortHandler.Write(packet, 0, packet.Length);
        }

        private void LiftStatusReceived(object sender, int status)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _liftStatus = status;
                RaisePropertyChanged("IsEnabledL1");
                RaisePropertyChanged("IsEnabledL2");
                RaisePropertyChanged("IsEnabledL3");
                RaisePropertyChanged("IsEnabledL4");
            });
        }

        private void RxDataReceived(object sender, int e)
        {
            List<byte> tmpBuilder = new List<byte>();
            for (int i = 0; i < e; i++)
            {
                if (_rxBuffer.TryDequeue(out byte data))
                {
                    if(!_rxLiftStatus.EvalNextByte(data))
                    {
                        tmpBuilder.AddRange(_rxLiftStatus.Consumed);
                        tmpBuilder.Add(data);
                    }
                }
            }
            _receivedTraceCharacters.AddRange(tmpBuilder);
            System.Windows.Application.Current.Dispatcher.Invoke(
                () =>
                {
                    _traceCharacters.AddRange<string>(
                        _computeTraceLines(tmpBuilder));
                });

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

        public static byte[] PackAvrMessage(byte[] payload, AvrPacketType packetType)
        {
            Contract.Requires(payload != null, "Payload must not be null");
            Contract.Requires(payload.Length <= 8, "Avr Frame must not be longer than 8 bytes");
            
            List<byte> packet = new List<byte>(payload.Length + 2)
            {
                (byte)packetType,
                (byte)(payload.Length + 2),
            };
            packet.AddRange(payload);
            return packet.ToArray();
        }
    }
}
