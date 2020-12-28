using System;   
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using System.Management;
using RuntimeCheck;

namespace AvrTerminal
{
    class SerialPortHandler
    {
        Thread _runner;
        AutoResetEvent _awaitPortOpen = new AutoResetEvent(false);
        AutoResetEvent _awaitDataAvailable = new AutoResetEvent(false);
        SerialPort _com;
        bool _running = false;
        static int[] _availableBaudRates = new[] { 250000, 500000, 1000000, 38400  };
        ConcurrentQueue<byte> _rxBuffer;
        object _synchRoot = new object();
        List<byte> _debugBuffer = new List<byte>();

        public SerialPortHandler(ConcurrentQueue<byte> buffer)
        {
            _rxBuffer = buffer;
            
        }

        public void Reset()
        {
            _debugBuffer.Clear();
        }

        public bool PortIsOpen
        {
            get
            {
                return _running;
            }
        }

        public void Write(byte[] data, int offset, int count)
        {
            
            if (PortIsOpen)
            {
                _com.Write(data, offset, count);
                _com.BaseStream.Flush();
            }
        }

        public event EventHandler<int> DataReceived;
        public event EventHandler<bool> PortStatusChanged;

        public void StartCom(string comPort, int baudRate)
        {
            _runner = new Thread(() =>
            {
                if (_com == null && !_running)
                {
                    _com = new SerialPort(comPort, baudRate);
                    
                    _com.ReadTimeout = 100;
                    try
                    {
                        _com.Open();
                        _com.DataReceived += _com_DataReceived;
                        _running = true;
                        NotifyPortStatusChanged();

                    }
                    catch { }

                    _awaitPortOpen.Set();
                    
                    try
                    {
                        while (_running)
                        {
                            _awaitDataAvailable.WaitOne(100);
                            int nrOfReceivedData = 0;
                            while (_com.BytesToRead > 0 && nrOfReceivedData < 1024)
                            {
                                byte data = (byte)_com.ReadByte();
                                _debugBuffer.Add(data);
                                _rxBuffer.Enqueue(data);
                                
                                nrOfReceivedData++;
                            }
                            NotifyDataReceived(nrOfReceivedData);
                            nrOfReceivedData = 0;
                        }
                    }
                    catch(ThreadAbortException)
                    {
                        try
                        {
                            _com.DataReceived -= _com_DataReceived;
                            _com.Dispose();
                        }
                        catch { }
                        _com = null;
                        _running = false;
                    }
                    catch {
                        _running = false;
                    }
                }
                try
                {

                    _com.DataReceived -= _com_DataReceived;
                    //_com.DiscardInBuffer();
                    //_com.DiscardOutBuffer();
                    //_com.Close();
                    // in case there is an exception, it should be caught in the global handler!
                    _com.Dispose();
                }
                catch { }
                _com = null;
                _runner = null;
                _running = false;
                NotifyPortStatusChanged();
            })
            {
                IsBackground = true, Priority = ThreadPriority.AboveNormal
            };
            _runner.Start();
            _awaitPortOpen.WaitOne(1000);

        }

        private void _com_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _awaitDataAvailable.Set();
        }

        public static string[] AvailablePorts
        {
            get
            {
                return System.IO.Ports.SerialPort.GetPortNames();
            }
        }

        public static int[] AvailableBaudRates
        {
            get
            {
                return _availableBaudRates;
            }
        }
        internal void StopCom()
        {
            if (_runner != null && _running)
            {
                lock (_synchRoot)
                {
                    _running = false;
                }
                if(!_runner.Join(500))
                {
                    _runner.Abort();
                }
            }

        }
         
        void NotifyDataReceived(int nrOfReceivedBytes)
        {
            if (nrOfReceivedBytes > 0 && DataReceived != null)
            {
                DataReceived(this, nrOfReceivedBytes);
            }
        }

        void NotifyPortStatusChanged()
        {
            if( PortStatusChanged != null)
            {
                PortStatusChanged(this, _running);
            }
        }
    }
}
