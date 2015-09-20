using Even.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Even.Tests.Utils
{
    public class EnumerableExtensionsTests
    {
        [Fact]
        public void IsSequential_returns_false_for_empty_enumerables()
        {
            Assert.False(new int[0].IsSequential());
        }

        public static IEnumerable<object[]> SequentialValues()
        {
            yield return new object[] { new [] { 0 } };
            yield return new object[] { new [] { 1000 } };
            yield return new object[] { new [] { 0, 1 } };
            yield return new object[] { new [] { 0, 1, 2, 3, 4, 5 } };
            yield return new object[] { new [] { 1000, 1001, 1002, 1003 } };
            yield return new object[] { new [] { Int32.MinValue, Int32.MinValue + 1, Int32.MinValue + 2 } };
            yield return new object[] { new [] { Int32.MaxValue - 2, Int32.MaxValue - 1, Int32.MaxValue } };
            yield return new object[] { new [] { -10, -9, -8, -7 } };
            yield return new object[] { new [] { -2, -1, 0, 1, 2 } };
        }

        [Theory]
        [MemberData("SequentialValues")]
        public void IsSequential_returns_true(int[] values)
        {
            Assert.True(values.IsSequential());
        }

        public static IEnumerable<object[]> NonSequentialValues()
        {
            yield return new object[] { new [] { 1, 2, 3, 5, 6, 7 } };
            yield return new object[] { new [] { 1000, 1001, 1002, 1030 } };
            yield return new object[] { new [] { Int32.MinValue, Int32.MaxValue } };
            yield return new object[] { new [] { 1, 0, -1 } };
        }

        [Theory]
        [MemberData("NonSequentialValues")]
        public void IsSequential_returns_false(int[] values)
        {
            Assert.False(values.IsSequential());
        }
    }
}
