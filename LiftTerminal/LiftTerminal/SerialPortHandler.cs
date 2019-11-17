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

namespace LiftTerminal
{
    class SerialPortHandler
    {
        Thread _runner;
        AutoResetEvent _awaitPortOpen = new AutoResetEvent(false);
        SerialPort _com;
        bool _running = false;
        static int[] _availableBaudRates = new[] { 38400 };
        ConcurrentQueue<byte> _rxBuffer;
        object _synchRoot = new object();

        public SerialPortHandler(ConcurrentQueue<byte> buffer)
        {
            _rxBuffer = buffer;
            
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
            }
        }

        public event EventHandler<int> DataReceived;
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
                        _running = true;
                    }
                    catch { }

                    _awaitPortOpen.Set();
                    int nrOfReceivedData = 0;
                    try
                    {
                        while (_running)
                        {
                            while (_com.BytesToRead > 0)
                            {
                                _rxBuffer.Enqueue((byte)_com.ReadByte());
                                nrOfReceivedData++;
                            }
                            NotifyDataReceived(nrOfReceivedData);
                            nrOfReceivedData = 0;
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                    catch(ThreadAbortException)
                    {
                        _com.DiscardInBuffer();
                        _com.DiscardOutBuffer();
                        _running = false;
                    }
                    catch {
                        _running = false;
                    }
                }
                
                // in case there is an exception, it should be caught in the global handler!
                _com.Dispose();
                _com = null;
                _runner = null;
                _running = false;
                
            })
            {
                IsBackground = true
            };
            _runner.Start();
            _awaitPortOpen.WaitOne(1000);

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

       
    }
}
