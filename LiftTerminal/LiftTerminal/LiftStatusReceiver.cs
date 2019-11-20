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

        RxState _currentState = RxState.WaitForStatusInfo;
        int _status = 0;
        public LiftStatusReceiver() {}

        public void EvalNextByte(byte b)
        {
            switch(_currentState)
            {
                case RxState.WaitForStatusInfo:
                    if( b == (byte)AvrPacketType.LiftStatus)
                    {
                        _currentState = RxState.WaitForLen;
                    }
                    break;
                case RxState.WaitForLen:
                    _currentState = RxState.WaitForStatusInfo;
                    if( b == 6)
                    {
                        _currentState = RxState.WaitForSynch1;
                    }
                    break;
                case RxState.WaitForSynch1:
                    _currentState = RxState.WaitForStatusInfo;
                    if (b == 0xA5)
                    {
                        _currentState = RxState.WaitForSynch2;
                    }
                    break;
                case RxState.WaitForSynch2:
                    _currentState = RxState.WaitForStatusInfo;
                    if (b == 0x5A)
                    {
                        _currentState = RxState.WaitForStatus;
                    }
                    break;
                case RxState.WaitForStatus:
                    _status = b;
                    _currentState = RxState.WaitForDoorOpen;
                    break;
                case RxState.WaitForDoorOpen:
                    _status = (_status << 8) | b;
                    NotifyStatusReceived();
                    _currentState = RxState.WaitForStatusInfo;
                    _status = 0;
                    break;
            }
        }

        public event EventHandler<int> StatusReceived;

        private void NotifyStatusReceived()
        {
            if( StatusReceived != null)
            {
                StatusReceived(this, _status);
            }
        }
    }
}
