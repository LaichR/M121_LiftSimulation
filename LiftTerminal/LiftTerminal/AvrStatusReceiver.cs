using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AvrTerminal
{
    class AvrStatusReceiver
    {
        enum RxState
        {
            WaitForStatusInfo,
            WaitForLen,
            WaitForSynch1,
            WaitForSynch2,
            WaitForStatus,
            WaitForDoorOpen,
            WaitForTraceMsgLen,
            RxTraceMessage,
            WaitRxReadRegLen,
            WaitRxReadRegData,
            WaitRxWriteRegStatus,
        }
        List<byte> _consumedHeader = new List<byte>();
        List<byte> _traceMessage = new List<byte>();
        List<byte> _regData = new List<byte>(4);
        int _expectedTraceBytes = 0;
        int _receivedTraceBytes = 0;
        int _expectedRegisterBytes = 0;
        int _receivedRegisterBytes = 0;
        RxState _currentState = RxState.WaitForStatusInfo;
        int _status = 0;

        public AvrStatusReceiver() {}

        public IEnumerable<byte> GetConsumed()
        {
            List<byte> ret = new List<byte>();
            switch(_currentState)
            {
                case RxState.WaitForLen:
                    if( _consumedHeader.Last() == 4)
                    {
                        ret.Add(_consumedHeader.First());
                        _consumedHeader.RemoveAt(0);
                    }
                    break;
                default:
                    ret.AddRange( _consumedHeader );
                    _consumedHeader.Clear();
                    break;
            }
            return ret;
        }

        public bool EvalNextByte(byte b)
        {
            bool ret = false;
            _consumedHeader.Add(b);
            switch(_currentState)
            {
                case RxState.WaitForStatusInfo:
                    if( b == (byte)AvrPacketType.LiftStatus)
                    {
                        _currentState = RxState.WaitForLen;
                        ret = true;
                    }
                    else if(b == (byte)AvrPacketType.TraceMessage)
                    {
                        _currentState = RxState.WaitForTraceMsgLen;
                        ret = true;
                    }
                    else if( b == (byte)AvrPacketType.ReadRegister)
                    {
                        _currentState = RxState.WaitRxReadRegLen;
                    }
                    else if( b == (byte)AvrPacketType.WriteRegister)
                    {
                        _currentState = RxState.WaitRxWriteRegStatus;
                    }
                    break;
                case RxState.WaitRxReadRegLen:
                    _expectedRegisterBytes = b;
                    _regData.Clear();
                    if (_expectedRegisterBytes == 0) // there was a problem
                    {
                        NotifyRegisterDataReceived();
                        _currentState = RxState.WaitForStatusInfo;
                    }
                    else
                    {
                        _receivedRegisterBytes = 0;
                        _currentState = RxState.WaitRxReadRegData;

                    }
                    break;
                case RxState.WaitRxReadRegData:
                    _regData.Add(b);
                    _receivedRegisterBytes++;
                    if( _receivedRegisterBytes == _expectedRegisterBytes)
                    {
                        _currentState = RxState.WaitForStatusInfo;
                        NotifyRegisterDataReceived();
                    }

                    break;
                case RxState.WaitRxWriteRegStatus:
                    bool status = b == 1;
                    _currentState = RxState.WaitForStatusInfo;
                    NotifyWriteRegStatus(status);
                    break;
                case RxState.WaitForLen:
                    _currentState = RxState.WaitForStatusInfo;
                    if( b == 6)
                    {
                        _currentState = RxState.WaitForSynch1;
                        ret = true;
                    }
                    else if( b == 4 )
                    {
                        _currentState = RxState.WaitForLen;
                    }
                    break;
                case RxState.WaitForSynch1:
                    _currentState = RxState.WaitForStatusInfo;
                    if (b == 0xA5)
                    {
                        _currentState = RxState.WaitForSynch2;
                        ret = true;
                    }
                    break;
                case RxState.WaitForSynch2:
                    _currentState = RxState.WaitForStatusInfo;
                    if (b == 0x5A)
                    {
                        _currentState = RxState.WaitForStatus;
                        ret = true;
                    }
                    break;
                case RxState.WaitForStatus:
                    _status = b;
                    _currentState = RxState.WaitForDoorOpen;
                    ret = true;
                    break;
                case RxState.WaitForDoorOpen:
                    _status = (_status << 8) | b;
                    NotifyStatusReceived();
                    Consumed.Clear();
                    _currentState = RxState.WaitForStatusInfo;
                    _status = 0;
                    ret = true;
                    break;
                case RxState.WaitForTraceMsgLen:
                    if (( b & (byte)AvrPacketType.TraceMassagePadLen) == (byte)AvrPacketType.TraceMassagePadLen )
                    {
                        _currentState = RxState.RxTraceMessage;
                        _expectedTraceBytes = (b & 7) + 2; // the id is implicit
                        _receivedTraceBytes = 0;
                        _traceMessage.Clear();
                        ret = true;
                    }
                    else
                    {
                        _currentState = RxState.WaitForStatusInfo;
                        Consumed.Clear();
                    }
                    break;
                case RxState.RxTraceMessage:
                    _receivedTraceBytes++;
                    _traceMessage.Add(b);
                    if( _receivedTraceBytes == _expectedTraceBytes )
                    {
                        _consumedHeader.Clear();
                        _currentState = RxState.WaitForStatusInfo;
                        NotifyTrace();
                    }
                    ret = true;
                    break;

            }
            return ret;
        }

        public event EventHandler<int> StatusReceived;
        public event EventHandler<byte[]> TraceMessageReceived;
        public event EventHandler<byte[]> RegisterDataReceived;
        public event EventHandler<bool> RegWriteStatusReceived;

        public List<byte> Consumed
        {
            get => _consumedHeader;
        }

        private void NotifyStatusReceived()
        {
            if( StatusReceived != null)
            {
                StatusReceived(this, _status);
            }
        }

        private void NotifyRegisterDataReceived()
        {
            if( RegisterDataReceived != null)
            {
                RegisterDataReceived(this, _regData.ToArray());
            }
        }

        private void NotifyWriteRegStatus(bool status)
        {
            if( RegWriteStatusReceived != null)
            {
                RegWriteStatusReceived(this, status);
            }
        }

        private void NotifyTrace()
        {
            if(TraceMessageReceived != null)
            {
                TraceMessageReceived(this, _traceMessage.ToArray());
            }
        }
    }
}
