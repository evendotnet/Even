using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests
{
    public class StreamTests
    {
        [Fact]
        public void Same_name_generates_equal_streams()
        {
            var a = new Stream("123");
            var b = new Stream("123");

            Assert.Equal(a, b);
        }

        [Fact]
        public void Names_must_be_case_insensitive()
        {
            var a = new Stream("foobar");
            var b = new Stream("FooBAR");

            Assert.Equal(a, b);
        }

        [Fact]
        public void Same_hashes_generate_equal_streams()
        {
            var hash1 = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
            var hash2 = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();

            var a = new Stream(hash1);
            var b = new Stream(hash2);

            Assert.Equal(a, b);
        }

        [Fact]
        public void Different_names_generate_different_hashes()
        {
            var a = new Stream("abc");
            var b = new Stream("xyz");

            Assert.NotEqual(a, b);
        }
    }
}
