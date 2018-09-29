using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI
{
    public class FileDTO
    {
        /// <summary>
        /// Google file identifier
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Google file name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Identifier of a permission that represents an ownership
        /// </summary>
        public string OwnershipPermissionId { get; set; }
    }
}
