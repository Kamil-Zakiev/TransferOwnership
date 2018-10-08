using System;
using System.Threading;

namespace UI
{
    public interface ITimeoutService
    {
        int GetTimeout();

        int CallTime { get; }

        void Timeout(int times=1);
    }

    class TimeoutService : ITimeoutService
    {
        private readonly ITimeoutValueProvider _timeoutValueProvider;
        private readonly ILogger _logger;

        public TimeoutService(ITimeoutValueProvider timeoutValueProvider, ILogger logger)
        {
            _timeoutValueProvider = timeoutValueProvider ?? throw new ArgumentNullException(nameof(timeoutValueProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public int CallTime { get; private set; } = 0;

        public void Timeout(int times = 1)
        {
            var ms = times * GetTimeout();
            CallTime += times;
            _logger.LogMessage($"Поток исполнения заморожен на {ms}ms.");
            Thread.Sleep(ms);
        }

        public int GetTimeout() => _timeoutValueProvider.GetTimeout();
    }
}
