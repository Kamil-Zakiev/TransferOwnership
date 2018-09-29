using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI
{
    public class TransferingResult
    {
        public bool Success => Exception == null;

        public FileDTO File { get; set; }

        public Exception Exception { get; set; }
    }
}
