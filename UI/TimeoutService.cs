using System;
using System.Threading;

namespace UI
{
    public interface ITimeoutService
    {
        int GetTimeout();

        int CallTime { get; }

        void Timeout();
    }

    class TimeoutService : ITimeoutService
    {
        private readonly ITimeoutValueProvider _timeoutValueProvider;

        public TimeoutService(ITimeoutValueProvider timeoutValueProvider)
        {
            _timeoutValueProvider = timeoutValueProvider ?? throw new ArgumentNullException(nameof(timeoutValueProvider));
        }
        public int CallTime { get; private set; } = 0;

        public void Timeout()
        {
            CallTime++;
            Thread.Sleep(GetTimeout());
        }

        public int GetTimeout() => _timeoutValueProvider.GetTimeout();
    }
}
