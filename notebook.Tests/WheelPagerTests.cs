using notebook.Services;

namespace notebook.Tests
{
    /// <summary>滾輪換月：撥一下只換一個月的自動測試。timestamp 是毫秒。</summary>
    public class WheelPagerTests
    {
        private static int[] Feed(WheelPager p, params (int Delta, int Time)[] events) =>
            events.Select(e => p.OnWheel(e.Delta, e.Time)).ToArray();

        /// <summary>一般滑鼠撥一下：從 start 開始，每 interval 毫秒一格，共 notches 格。</summary>
        private static (int, int)[] Flick(int start, int notches, int interval = 20, int delta = -120) =>
            Enumerable.Range(0, notches).Select(i => (delta, start + i * interval)).ToArray();

        [Fact]
        public void SingleNotch_PagesOnce()
        {
            var p = new WheelPager();
            Assert.Equal(1, p.OnWheel(-120, 1000));  // 往下 = 下一個月
            Assert.Equal(-1, p.OnWheel(120, 2000));  // 停一下再往上 = 上一個月
        }

        [Fact]
        public void OneFlick_ManyNotches_PagesOnlyOnce()
        {
            var p = new WheelPager();
            var steps = Feed(p, Flick(1000, 6));

            Assert.Equal([1, 0, 0, 0, 0, 0], steps);
        }

        [Fact]
        public void RepeatedFlicks_EachFlickPages()
        {
            // 一般滑鼠連續撥好幾下，中間手指抬起來換位置的空檔約 0.2 秒：每撥一下換一個月，不會卡住
            var p = new WheelPager();
            var events = Flick(1000, 4).Concat(Flick(1260, 4)).Concat(Flick(1520, 4)).ToArray();

            Assert.Equal(3, Feed(p, events).Count(s => s == 1));
        }

        [Fact]
        public void ContinuousSpin_KeepsPagingAtSteadyRate()
        {
            // 一直滾不停（事件間隔都很短）：不會卡住，大約每 RepeatMs 換一頁
            var p = new WheelPager();
            var steps = Feed(p, Flick(1000, 100, interval: 20)); // 2 秒

            int pages = steps.Count(s => s == 1);
            Assert.InRange(pages, 2000 / WheelPager.RepeatMs, 2000 / WheelPager.RepeatMs + 1);
        }

        [Fact]
        public void SlowNotchByNotch_EachNotchPages()
        {
            var p = new WheelPager();
            var steps = Feed(p, Flick(1000, 4, interval: WheelPager.GapMs + 50));

            Assert.Equal([1, 1, 1, 1], steps);
        }

        [Fact]
        public void AfterPause_NextScrollPagesAgain()
        {
            var p = new WheelPager();
            Assert.Equal(1, p.OnWheel(-120, 1000));
            Assert.Equal(0, p.OnWheel(-120, 1100));
            Assert.Equal(1, p.OnWheel(-120, 1100 + WheelPager.GapMs + 1));
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

        [Fact]
        public void TimestampWraparound_StillWorks()
        {
            // 開機約 24.8 天後毫秒計數會變成負數，約 49.7 天後繞回
            var p = new WheelPager();
            Assert.Equal(1, p.OnWheel(-120, int.MaxValue - 10));
            Assert.Equal(0, p.OnWheel(-120, unchecked(int.MaxValue + 10)));  // 同一次滾動
            Assert.Equal(1, p.OnWheel(-120, unchecked(int.MaxValue + 10 + WheelPager.GapMs + 1)));
        }
    }
}
