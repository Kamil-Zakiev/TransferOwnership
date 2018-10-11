using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UI
{
    public class FormLoggerDecorator : ILogger
    {
        private TextBox _textBox;

        private LinkedList<string> _log = new LinkedList<string>();

        public FormLoggerDecorator(ILogger logger, TextBox textBox, TaskScheduler taskScheduler)
        {
            _textBox = textBox;
            _logger = logger;
            _taskScheduler = taskScheduler;
        }

        private ILogger _logger;
        private TaskScheduler _taskScheduler;

        public void LogMessage(string message)
        {
            _log.AddLast(message);

            if (_log.Count > 10)
            {
                _log.RemoveFirst();
            }

            var newLogStr = string.Join(Environment.NewLine, _log);
            var task = new Task(() =>  _textBox.Text = newLogStr);
            task.Start(_taskScheduler);

            _logger.LogMessage(message);
        }
    }
}
