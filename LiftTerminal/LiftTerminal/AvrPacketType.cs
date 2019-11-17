using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiftTerminal
{
    public enum AvrPacketType
    {
        Undefined = 0,
        TraceMessage = 1,
        LiftSimulatorButton = 2,
        TestCommand = 3
    }
}
