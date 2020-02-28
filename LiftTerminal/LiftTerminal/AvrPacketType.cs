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
        LiftSimulatorButton = 2,
        TestCommand = 3,
        LiftStatus = 4,
        ReadRegister = 6,
        WriteRegister = 8,
        TraceMessage = 0xA5,
        TraceMassagePadLen = 0xA8,

    }
}
