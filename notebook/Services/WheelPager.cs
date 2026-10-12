namespace notebook.Services
{
    /// <summary>
    /// 把滑鼠滾輪換成「一次滾動只換一頁」。
    /// 快速轉一下滾輪、無段滾輪或觸控板，一次動作會連續送出很多個滾輪事件；
    /// 如果每個事件都換一個月，一滑就會跳過好幾個月。
    /// 所以換頁之後，滾輪要先停下來超過 QuietMs 才算下一次動作，期間的事件都略過。
    /// 觸控板每個事件的量很小，累積滿一格（120）才換頁。
    /// </summary>
    public sealed class WheelPager
    {
        public const int NotchDelta = 120;
        public const int QuietMs = 250;

        private int _accumulated;
        private bool _paged;
        private long? _lastTimestamp;

        /// <summary>傳入一個滾輪事件（timestamp 為毫秒）。回傳 -1 上一頁、1 下一頁、0 不換頁。</summary>
        public int OnWheel(int delta, long timestamp)
        {
            if (_lastTimestamp is not long last || timestamp - last > QuietMs)
            {
                // 停過一下：新的一次動作
                _accumulated = 0;
                _paged = false;
            }
            _lastTimestamp = timestamp;

            if (_paged || delta == 0) return 0;

            // 方向反過來就重新累積
            if (Math.Sign(delta) != Math.Sign(_accumulated)) _accumulated = 0;
            _accumulated += delta;
            if (Math.Abs(_accumulated) < NotchDelta) return 0;

            _paged = true;
            return _accumulated > 0 ? -1 : 1; // 滾輪往上（正值）= 上一個月
        }
    }
}
