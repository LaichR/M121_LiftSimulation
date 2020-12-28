using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Builder.MetadataDescription;

namespace AvrTerminal
{
    class AvrRegAccess : IRegisterAccess
    {
        readonly object _syncRoot = new object();
        readonly AvrStatusReceiver _receiver;
        readonly SerialPortHandler _serialPort;
        readonly List<Byte> _writeBuffer = new List<byte>(8);
        readonly List<Byte> _readBuffer = new List<byte>(8);
        readonly ManualResetEvent _awaitReadRegStatus = new ManualResetEvent(false);
        readonly ManualResetEvent _awaitWriteRegStatus = new ManualResetEvent(false);
        volatile byte[] _receivedBuffer = null;
        volatile bool _writeRegStatus = false;

        public AvrRegAccess(SerialPortHandler serialPort,
            AvrStatusReceiver statusReceiver)
        {
            _serialPort = serialPort;
            
            _receiver = statusReceiver;
            _receiver.RegWriteStatusReceived += OnRegWriteStatusReceived;
            _receiver.RegisterDataReceived += OnRegisterDataReceived;
        }

        internal SerialPortHandler DeviceConnection => _serialPort;
        public bool CanAccessDevice => _serialPort.PortIsOpen;

        public byte[] Read(uint address, uint nrOfBytes)
        {
            _readBuffer.Clear();
            _readBuffer.Add((byte)AvrPacketType.ReadRegister);
            _readBuffer.Add((byte)5); // length of the payload + header
            _readBuffer.AddRange(BitConverter.GetBytes((ushort)address));
            _readBuffer.Add((byte)nrOfBytes);
            lock (_syncRoot)
            {
                _receivedBuffer = null;
            }
            _awaitReadRegStatus.Reset();
            _serialPort.Write(_readBuffer.ToArray(), 0, _readBuffer.Count);
            if (!_awaitReadRegStatus.WaitOne(5000))
            {
                _serialPort.Reset();
                throw new TimeoutException("Read register timed out");
                
            }
            //System.Threading.Thread.Sleep(5); 
            return _receivedBuffer;
            
        }

        public void Write(uint address, byte[] data)
        {
            _writeBuffer.Clear();
            _writeBuffer.Add((byte)AvrPacketType.WriteRegister);
            _writeBuffer.Add((byte)(5 + data.Length));
            _writeBuffer.AddRange(BitConverter.GetBytes((ushort)address));
            _writeBuffer.Add((byte)data.Length);
            _writeBuffer.AddRange(data);
            _writeRegStatus = false;
            _awaitWriteRegStatus.Reset();
            _serialPort.Write(_writeBuffer.ToArray(), 0, _writeBuffer.Count);
            if( !_awaitWriteRegStatus.WaitOne(5000))
            {
                _serialPort.Reset();
                throw new TimeoutException("Write register timed out!");
                
            }
            if( !_writeRegStatus)
            {
                throw new ApplicationException("Write register failed");
            }
        }

        private void OnRegisterDataReceived(object sender, byte[] data)
        {
            lock (_syncRoot)
            {
                _receivedBuffer = new byte[data.Length];
                Array.Copy(data, _receivedBuffer, data.Length);
                _awaitReadRegStatus.Set();
            }
        }

        private void OnRegWriteStatusReceived(object sender, bool status)
        {
            lock (_syncRoot)
            {
                _writeRegStatus = status;
                _awaitWriteRegStatus.Set();
            }
        }
    }
}
