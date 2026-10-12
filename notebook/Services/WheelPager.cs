namespace notebook.Services
{
    /// <summary>
    /// 把滑鼠滾輪換成「撥一下只換一頁」。
    /// 手指撥一下滾輪通常會轉過好幾格，連續送出好幾個滾輪事件；如果每個事件都換一個月，一撥就會跳過好幾個月。
    /// 所以：
    /// - 滾輪停超過 GapMs 才算新的一次滾動，新的一次可以馬上換頁；
    /// - 同一次滾動裡換過頁之後，要再過 RepeatMs 才會再換頁（一直滾不停時，月份會穩定地一頁一頁換，不會卡住）；
    /// - 觸控板每個事件的量很小，累積滿一格（120）才換頁。
    /// </summary>
    public sealed class WheelPager
    {
        public const int NotchDelta = 120;
        public const int GapMs = 150;
        public const int RepeatMs = 350;

        private int _accumulated;
        private int? _lastTime;
        private int? _pagedAt;

        /// <summary>
        /// 傳入一個滾輪事件（timestamp 為毫秒，可用 MouseWheelEventArgs.Timestamp，溢位繞回也沒關係）。
        /// 回傳 -1 上一頁、1 下一頁、0 不換頁。
        /// </summary>
        public int OnWheel(int delta, int timestamp)
        {
            if (_lastTime is not int last || Elapsed(last, timestamp) > GapMs)
            {
                // 停過一下：新的一次滾動
                _accumulated = 0;
                _pagedAt = null;
            }
            _lastTime = timestamp;

            if (delta == 0) return 0;
            if (_pagedAt is int paged && Elapsed(paged, timestamp) < RepeatMs) return 0;

            // 方向反過來就重新累積
            if (Math.Sign(delta) != Math.Sign(_accumulated)) _accumulated = 0;
            _accumulated += delta;
            if (Math.Abs(_accumulated) < NotchDelta) return 0;

            int step = _accumulated > 0 ? -1 : 1; // 滾輪往上（正值）= 上一個月
            _accumulated = 0;
            _pagedAt = timestamp;
            return step;
        }

        // 毫秒計數器約 49.7 天會繞回，用無號相減處理；時間倒退會變成很大的值，當作停過一下
        private static uint Elapsed(int from, int to) => unchecked((uint)(to - from));
    }
}
