using System;
using System.Configuration;

namespace UI
{
    interface ITimeoutValueProvider
    {
        int GetTimeout();
    }

    class ConfigTimeoutValueProvider : ITimeoutValueProvider
    {
        public int GetTimeout()
        {
            var timeoutStr = ConfigurationManager.AppSettings["timeout"];
            return Convert.ToInt32(timeoutStr);
        }
    }
}
