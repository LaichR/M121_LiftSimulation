using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvrTerminal
{
    public enum AvrPacketType
    {
        Undefined = 0,
        LiftSimulatorButton = 0xA2,
        TestCommand = 0xA3,
        LiftStatus = 0xA4,
        TraceMessage = 0xA5,
        ReadRegister = 0xA6,
        WriteRegister = 0xA8,
        TraceMassagePadLen = 0xA8,
    }
}
