using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI
{
    /// <summary>
    /// User's DTO
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// Owners email
        /// </summary>
        public string EmailAddress { get; set; }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Uri to a photo link
        /// </summary>
        public Uri PhotoLink { get; set; }
    }
}
