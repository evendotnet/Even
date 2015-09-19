using Even.Utils;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Even.Tests.Utils
{
    public class BatchStringBuilderTests
    {
        [Fact]
        public void Empty_input_array_yields_no_output()
        {
            var output = BatchStringBuilder.Build(new object[0]).ToList();

            Assert.Equal(0, output.Count);
        }

        [Fact]
        public void Single_input_yields_output()
        {
            var output = BatchStringBuilder.Build(new[] { "foo" }).ToList();
            Assert.Equal(1, output.Count);
        }

        [Fact]
        public void Multiple_inputs_yielrs_multiple_outputs()
        {
            var output = BatchStringBuilder.Build(new[] { 1, 2, 3, 4, 5 }, maxBatchSize: 2).ToList();

            Assert.Equal(3, output.Count);
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("abc")]
        [InlineData("abcabcabcabcabc")]
        public void Single_item_is_appended_no_matter_the_length(string item)
        {
            var output = BatchStringBuilder.Build(new[] { item }, desiredLength: 1).ToList();

            Assert.Equal(item, output[0].String);
        }

        [Fact]
        public void Multiple_items_output_in_desired_length()
        {
            var output = BatchStringBuilder.Build(new[] { "aaa", "bbb", "ccc", "ddd", "eee" }, desiredLength: 6).ToList();

            Assert.Equal(3, output.Count);
        }

        [Fact]
        public void Multiple_items_output_with_max_batch_size()
        {
            var output = BatchStringBuilder.Build(new[] { "aaa", "bbb", "ccc", "ddd", "eee" }, maxBatchSize: 2).ToList();

            Assert.Equal(3, output.Count);
        }

        public static IEnumerable<object[]> BatchSizeTestData()
        {
            yield return new object[] { new object[] { 11, 22, 33, 44, 55, 66 }, 1, new[] { "11", "22", "33", "44", "55", "66" } };
            yield return new object[] { new object[] { 11, 22, 33, 44, 55, 66 }, 2, new[] { "1122", "3344", "5566" } };
            yield return new object[] { new object[] { 11, 22, 33, 44, 55, 66 }, 3, new[] { "112233", "445566" } };
            yield return new object[] { new object[] { 11, 22, 33, 44, 55, 66 }, 4, new[] { "11223344", "5566" } };
            yield return new object[] { new object[] { 11, 22, 33, 44, 55, 66 }, 5, new[] { "1122334455", "66" } };
            yield return new object[] { new object[] { 11, 22, 33, 44, 55, 66 }, 6, new[] { "112233445566" } };
            yield return new object[] { new object[] { 11, 22, 33, 44, 55, 66 }, 7, new[] { "112233445566" } };
        }

        [Theory]
        [MemberData("BatchSizeTestData")]
        public void Batch_size_outputs_expected_strings(IEnumerable<object> items, int batchSize, string[] expected)
        {
            var output = BatchStringBuilder.Build(items, maxBatchSize: batchSize).Select(o => o.String).ToList();

            Assert.Equal(expected, output);
        }

        public static IEnumerable<object[]> DesiredLengthTestData()
        {
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 1, new[] { "11", "22", "33", "44" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 2, new[] { "11", "22", "33", "44" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 3, new[] { "11", "22", "33", "44" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 4, new[] { "1122", "3344" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 5, new[] { "1122", "3344" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 6, new[] { "112233", "44" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 7, new[] { "112233", "44" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 8, new[] { "11223344" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 9, new[] { "11223344" } };
            yield return new object[] { new object[] { 11, 22, 33, 44 }, 10, new[] { "11223344" } };
        }

        [Theory]
        [MemberData("DesiredLengthTestData")]
        public void Desider_length_outputs_expected_strings(IEnumerable<object> items, int desiredLEngth, string[] expected)
        {
            var output = BatchStringBuilder.Build(items, desiredLength: desiredLEngth).Select(o => o.String).ToList();

            Assert.Equal(expected, output);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void Prefix_is_appended_to_each_string(int batchSize)
        {
            var output = BatchStringBuilder.Build(new[] { 1, 2, 3, 4, 5, 6 }, maxBatchSize: batchSize, onStart: sb => sb.Append("a"));

            foreach (var o in output)
                Assert.StartsWith("a", o.String);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void Suffix_is_appended_to_each_string(int batchSize)
        {
            var output = BatchStringBuilder.Build(new[] { 1, 2, 3, 4, 5, 6 }, maxBatchSize: batchSize, onFinish: sb => sb.Append("a"));

            foreach (var o in output)
                Assert.EndsWith("a", o.String);
        }
    }
}
