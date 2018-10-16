using System;
using System.Collections.Generic;
using System.IO;

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
        /// Delete permission to disable view and edit a file
        /// </summary>
        void DeleteOwnershipPermission(IReadOnlyList<FileDTO> files);

        string UploadFile(FileDTO file, Stream stream);

        void RecoverParents(IReadOnlyList<FileDTO> dirs);

        void TrashFiles(IReadOnlyList<string> filesIdsToTrash);

        void RejectRights(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<FileDTO> callback);
    }
}
