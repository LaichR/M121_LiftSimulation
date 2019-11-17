using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestInterface
{
    /// <summary>
    /// Container for single Tests
    /// </summary>
    public interface ITestSuite : IEnumerator<ITest> { }

}
