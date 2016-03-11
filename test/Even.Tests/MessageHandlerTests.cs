using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class MessageHandlerTests
    {
        [Fact]
        public async Task Handles_messages()
        {
            var handler = new ObjectHandler();

            var expected = 42;
            int actual = 0;

            handler.AddHandler<object>(s => actual = 100);
            handler.AddHandler<string>(s => actual = expected);

            await handler.Handle("go");

            Assert.Equal(expected, actual);
        }

        class SingleChar { }
        class MultiChar { }

        [Fact]
        public async Task Handles_mapped_messages()
        {
            var handler = new MessageHandler<string>(s => s.Length == 1 ? typeof(SingleChar) : typeof(MultiChar));

            var expected = 42;
            int actual = 0;

            handler.AddHandler<MultiChar>(s => actual = 100);
            handler.AddHandler<SingleChar>(s => actual = expected);

            await handler.Handle("a");

            Assert.Equal(expected, actual);
        }
    }
}
