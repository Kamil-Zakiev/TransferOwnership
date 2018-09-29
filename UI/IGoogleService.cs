using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI
{
    /// <summary>
    /// Google Drive Service
    /// </summary>
    public interface IGoogleService
    {
        /// <summary>
        /// Get user's information
        /// </summary>
        /// <returns></returns>
        UserInfo GetUserInfo();

        /// <summary>
        /// Get files that owned by current user
        /// </summary>
        IReadOnlyList<FileDTO> GetOwnedFiles();

        /// <summary>
        /// Transfer ownership to newUser
        /// </summary>
        IReadOnlyList<TransferingResult> TransferOwnershipTo(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService);

        /// <summary>
        /// Delete permission to disable view and edit a file
        /// </summary>
        void DeleteOwnershipPermission(FileDTO file);
    }
}
