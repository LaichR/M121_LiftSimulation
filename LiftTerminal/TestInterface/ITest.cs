using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestInterface
{
    public interface ITest
    {
        string Name
        {
            get;
        }

        byte[] GetTestData(out int expectedResponseLength );
        
        bool ReceiveResponse(byte[] response);
    }
}
