using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
        [Trait("Category","Timeout")]
        public void Timeout_does_not_expires_before_time(int milliseconds)
        {
            var timeout = Timeout.In(TimeSpan.FromMilliseconds(milliseconds));

            Thread.Sleep(milliseconds - 10);

            Assert.False(timeout.IsExpired);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(250)]
        [InlineData(500)]
        [InlineData(1000)]
        [Trait("Category", "Timeout")]
        public void Timeout_is_expired_after_expected_time(int milliseconds)
        {
            var timeout = Timeout.In(TimeSpan.FromMilliseconds(milliseconds));

            Thread.Sleep(milliseconds + 1);

            Assert.True(timeout.IsExpired);
        }
    }
}
