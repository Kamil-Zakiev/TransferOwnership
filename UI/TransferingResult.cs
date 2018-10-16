using System;

namespace UI
{
    public class TransferingResult
    {
        public bool Success => Exception == null;

        public FileDTO File { get; set; }

        public Exception Exception { get; set; }
    }
}
