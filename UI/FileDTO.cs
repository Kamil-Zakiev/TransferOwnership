using System;
using System.Collections.Generic;
using Google.Apis.Drive.v3.Data;

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

        public string MimeType { get; set; }

        public IList<string> Parents { get; internal set; }

        public bool? ExplicitlyTrashed { get; set; }
        public IList<Permission> Permissions { get; internal set; }
    }
}
