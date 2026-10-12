using notebook.Services;

namespace notebook.Tests
{
    /// <summary>滾輪換月：一次滾動只換一個月的自動測試。timestamp 是毫秒。</summary>
    public class WheelPagerTests
    {
        private static int[] Feed(WheelPager p, params (int Delta, long Time)[] events) =>
            events.Select(e => p.OnWheel(e.Delta, e.Time)).ToArray();

        [Fact]
        public void SingleNotch_PagesOnce()
        {
            var p = new WheelPager();
            Assert.Equal(1, p.OnWheel(-120, 1000));  // 往下 = 下一個月
            Assert.Equal(-1, p.OnWheel(120, 2000));  // 停一下再往上 = 上一個月
        }

        [Fact]
        public void FastSpin_ManyEvents_PagesOnlyOnce()
        {
            var p = new WheelPager();
            var events = Enumerable.Range(0, 30).Select(i => (-120, 1000L + i * 15)).ToArray();

            var steps = Feed(p, events);

            Assert.Equal(1, steps.Count(s => s != 0));
            Assert.Equal(1, steps[0]);
        }

        [Fact]
        public void LongContinuousSpin_StillOnePage()
        {
            // 無段滾輪一直轉（事件間隔都短於 QuietMs），總時間超過 QuietMs 也只換一次
            var p = new WheelPager();
            var events = Enumerable.Range(0, 100).Select(i => (-120, 1000L + i * 100)).ToArray();

            Assert.Equal(1, Feed(p, events).Count(s => s != 0));
        }

        [Fact]
        public void AfterPause_NextScrollPagesAgain()
        {
            var p = new WheelPager();
            Assert.Equal(1, p.OnWheel(-120, 1000));
            Assert.Equal(0, p.OnWheel(-120, 1100));
            Assert.Equal(1, p.OnWheel(-120, 1100 + WheelPager.QuietMs + 1));
        }

        [Fact]
        public void Touchpad_SmallDeltas_AccumulateToOneNotch()
        {
            var p = new WheelPager();
            var steps = Feed(p, (-40, 1000), (-40, 1010), (-40, 1020), (-40, 1030), (-40, 1040));

            Assert.Equal([0, 0, 1, 0, 0], steps);
        }

        [Fact]
        public void Touchpad_TinyGesture_DoesNotPage()
        {
            var p = new WheelPager();
            Assert.Equal([0, 0], Feed(p, (-30, 1000), (-30, 1010)));
            // 停下來之後重新累積，不會跟前一次的小動作加在一起
            Assert.Equal([0, 0], Feed(p, (-30, 2000), (-30, 2010)));
        }

        [Fact]
        public void DirectionChange_RestartsAccumulation()
        {
            var p = new WheelPager();
            Assert.Equal([0, 0, -1], Feed(p, (-100, 1000), (60, 1010), (60, 1020)));
        }
    }
}
