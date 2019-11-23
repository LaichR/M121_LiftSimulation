using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiftTerminal
{
    class LiftStatusReceiver
    {
        enum RxState
        {
            WaitForStatusInfo,
            WaitForLen,
            WaitForSynch1,
            WaitForSynch2,
            WaitForStatus,
            WaitForDoorOpen
        }
        List<byte> _consumedHeader = new List<byte>();
        RxState _currentState = RxState.WaitForStatusInfo;
        int _status = 0;
        public LiftStatusReceiver() {}

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
                    break;
                case RxState.WaitForLen:
                    _currentState = RxState.WaitForStatusInfo;
                    if( b == 6)
                    {
                        _currentState = RxState.WaitForSynch1;
                        ret = true;
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
            }
            return ret;
        }

        public event EventHandler<int> StatusReceived;

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
    }
}
