using DocExtractor.ML.Recommendation;
using Xunit;

namespace DocExtractor.Tests.Recommendation
{
    public class RecommenderTests
    {
        [Fact]
        public void CharBigramJaccard_ShouldReturnOne_WhenStringsEqual()
        {
            var score = GroupItemRecommender.CharBigramJaccard("遥测参数", "遥测参数");
            Assert.Equal(1.0f, score, 3);
        }

        [Fact]
        public void CharBigramJaccard_ShouldReturnZero_WhenNoBigramOverlap()
        {
            var score = GroupItemRecommender.CharBigramJaccard("ABCD", "WXYZ");
            Assert.Equal(0f, score, 3);
        }

        [Fact]
        public void CharBigramJaccard_ShouldSupportChineseText()
        {
            var score = GroupItemRecommender.CharBigramJaccard("电源系统", "电源模块");
            Assert.True(score > 0f);
        }
    }
}
