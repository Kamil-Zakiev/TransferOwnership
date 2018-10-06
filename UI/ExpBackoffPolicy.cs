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
        private const int RetryCount = 4;

        public void GrantedDelivery(Action action, Action replacingAction = null)
        {
            var attemptNumber = 0;

            while (true)
            {
                try
                {
                    attemptNumber++;
                    action();
                    return;
                }
                catch (Exception)
                {
                    if (attemptNumber > RetryCount)
                    {
                        throw;
                    }

                    if (replacingAction != null)
                    {
                        action = replacingAction;
                    }

                    var sleepSeconds = Math.Pow(2, attemptNumber);
                    Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                }
            }
        }
    }
}
