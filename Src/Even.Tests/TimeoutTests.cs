using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class TimeoutTests
    {
        [Theory]
        [InlineData(100)]
        [InlineData(250)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task Timeout_does_not_expires_before_time(int milliseconds)
        {
            var timeout = Timeout.In(TimeSpan.FromMilliseconds(milliseconds));
            
            await Task.Delay(milliseconds - 50);

            Assert.False(timeout.IsExpired);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(250)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task Timeout_is_expired_after_expected_time(int milliseconds)
        {
            var timeout = Timeout.In(TimeSpan.FromMilliseconds(milliseconds));

            await Task.Delay(milliseconds);

            Assert.True(timeout.IsExpired);
        }
    }
}
