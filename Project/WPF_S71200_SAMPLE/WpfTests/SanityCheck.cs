using Xunit;

namespace WpfTests
{
    public class SanityCheck
    {
        [Fact]
        public void OnePlusOne_EqualsTwo()
        {
            Assert.Equal(2, 1 + 1);
        }
    }
}
