using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiftTerminal
{
    [Flags]
    enum ButtonType
    {
        Undefined,
        Floor = 2,
        Cabine = 4
    }
}
