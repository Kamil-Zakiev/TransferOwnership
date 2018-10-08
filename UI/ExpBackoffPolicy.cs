using System;
using System.Threading;

namespace UI
{
    public interface IExpBackoffPolicy
    {
        void GrantedDelivery(Action action, Action replacingAction = null);
    }

    internal class ExpBackoffPolicy : IExpBackoffPolicy
    {
        private ILogger _logger;

        public ExpBackoffPolicy(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private const int RetryCount = 5;

        public void GrantedDelivery(Action action, Action replacingAction = null)
        {
            var attemptNumber = 0;

            while (true)
            {
                try
                {
                    attemptNumber++;
                    _logger.LogMessage($"Попытка запуска № {attemptNumber}.");
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    var message = Helpers.GetFullMessage(ex);
                    _logger.LogMessage($"Ошибка при запуске № {attemptNumber}: " + message);
                    if (attemptNumber > RetryCount)
                    {
                        throw;
                    }

                    if (replacingAction != null)
                    {
                        action = replacingAction;
                    }

                    var sleepSeconds = Math.Pow(2, attemptNumber);
                    _logger.LogMessage($"Усыпляем поток выполнения на {sleepSeconds} секунд.");
                    Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                }
            }
        }
    }
}
