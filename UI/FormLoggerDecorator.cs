using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace UI
{
    public class FormLoggerDecorator : ILogger
    {
        private TextBox _textBox;

        private LinkedList<string> _log = new LinkedList<string>();

        public FormLoggerDecorator(ILogger logger, TextBox textBox)
        {
            _textBox = textBox;
            _logger = logger;
        }

        private ILogger _logger;

        public void LogMessage(string message)
        {
            _log.AddLast(message);

            if (_log.Count > 10)
            {
                _log.RemoveFirst();
            }

            var newLogStr = string.Join(Environment.NewLine, _log);
            _textBox.Text = newLogStr;
            _logger.LogMessage(message);
        }
    }
}
