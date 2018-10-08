using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI
{
    public static class Helpers
    {
        public static string GetFullMessage(Exception ex)
        {
            var messages = new List<string>();
            Helpers.FillExceptionMessages(messages, ex);
            return string.Join(Environment.NewLine, messages.Distinct());
        }

        private static void FillExceptionMessages(List<string> messages, Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(exception.Message))
            {
                messages.Add(exception.Message);
            }

            if (exception.InnerException != null)
            {
                FillExceptionMessages(messages, exception.InnerException);
            }

            if (exception is AggregateException aggregateException && aggregateException.InnerExceptions != null)
            {
                foreach (var ex in aggregateException.InnerExceptions)
                {
                    FillExceptionMessages(messages, ex);
                }
            }
        }
    }
}
