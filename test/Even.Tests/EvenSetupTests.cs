using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Even;

namespace Even.Tests
{
    public class EvenSetupTests : EvenTestKit
    {
        [Fact]
        public async Task Start_with_default_config_works()
        {
            await Sys.SetupEven().Start();
        }
    }
}
