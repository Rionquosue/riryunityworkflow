#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Riry.RiryTools.Editor.Tests
{
    public sealed class PlayModeWindowKeeperTests
    {
        [Test]
        public void WindowIdsRoundTripAndIgnoreDamagedEntries()
        {
            string text = PlayModeWindowKeeper.JoinIds(new[] { 12, -40, 7 });
            Assert.That(PlayModeWindowKeeper.SplitIds(text), Is.EqualTo(new[] { 12, -40, 7 }));
            Assert.That(PlayModeWindowKeeper.SplitIds(""), Is.Empty);
            Assert.That(PlayModeWindowKeeper.SplitIds("3,,x,4"), Is.EqualTo(new[] { 3, 4 }));
        }
    }
}
#endif
