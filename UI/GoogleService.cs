using Google.Apis.Drive.v3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace UI
{
    /// <summary>
    /// Adapter to a Google.Apis.Drive.v3.DriveService
    /// </summary>
    public class GoogleService : IGoogleService
    {
        private DriveService _driveService;

        public GoogleService(DriveService driveService)
        {
            _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        }

        public IReadOnlyList<FileDTO> GetOwnedFiles()
        {
            var listRequest = _driveService.Files.List();

            listRequest.Fields = "files(owners,ownedByMe,name,id,mimeType,parents,explicitlyTrashed)";
            var files = listRequest.Execute().Files;

            var currentUser = GetUserInfo();

            return files
                .Where(file => file.OwnedByMe.HasValue && file.OwnedByMe.Value)
                .Select(f => new FileDTO
                {
                    Id = f.Id,
                    Name = f.Name,
                    OwnershipPermissionId = f.Owners.First(owner => owner.EmailAddress == currentUser.EmailAddress).PermissionId,
                    MimeType = f.MimeType,
                    Parents = f.Parents,
                    ExplicitlyTrashed = f.ExplicitlyTrashed
                })
                .ToArray();
        }

        public UserInfo GetUserInfo()
        {
            //todo: put defaultUserUrl to config
            const string defaultUserUrl = "https://icon-icons.com/icons2/1378/PNG/512/avatardefault_92824.png";
            var aboutGet = _driveService.About.Get();
            aboutGet.Fields = "user";

            var user = aboutGet.Execute().User;
            return new UserInfo
            {
                EmailAddress = user.EmailAddress,
                Name = user.DisplayName,
                PhotoLink = user.PhotoLink != null ? new Uri(user.PhotoLink) : new Uri(defaultUserUrl)
            };
        }

        public void DeleteOwnershipPermission(FileDTO file)
        {
            var deleteCommand = _driveService.Permissions.Delete(file.Id, file.OwnershipPermissionId);
            deleteCommand.Execute();
        }

        public IReadOnlyList<TransferingResult> TransferOwnershipTo(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<int, FileDTO> callback)
        {
            var newOwner = newOwnerGoogleService.GetUserInfo();

            //var mentionedParents = files.SelectMany(f => f.Parents).ToArray();

            var dirs = files.Where(f => f.MimeType == "application/vnd.google-apps.folder").ToArray();

            files = dirs.Concat(files.Except(dirs)).ToArray();

            var commandsDto = files
                .Select(file =>
                {
                    var command = _driveService.Permissions.Create(new Google.Apis.Drive.v3.Data.Permission
                    {
                        Role = "owner",
                        Type = "user",
                        EmailAddress = newOwner.EmailAddress
                    }, file.Id);

                    command.TransferOwnership = true;

                    // google does not allow to skip notification
                    // command.SendNotificationEmail = false;

                    return new { command, file };
                })
                .ToArray();

            var result = new List<TransferingResult>();
            for (int i = 0; i < commandsDto.Length; i++)
            {
                var commandDto = commandsDto[i];
                // introducing new owner of a file
                var command = commandDto.command;

                try
                {
                    command.Execute();
                    callback(i, commandDto.file);
                }
                catch (Exception e)
                {
                    result.Add(new TransferingResult
                    {
                        Exception = e,
                        File = commandDto.file
                    });
                    continue;
                }

                // removing view and edit permissions of an old owner from new user authority
                var file = commandDto.file;
                newOwnerGoogleService.DeleteOwnershipPermission(file);

                result.Add(new TransferingResult
                {
                    File = commandDto.file
                });
            }

            // correct dirs chain
            newOwnerGoogleService.RecoverParents(dirs);

            return result;
        }

        private string GetRootFolderId()
        {
            var rootgetCommand = _driveService.Files.Get("root");
            rootgetCommand.Fields = "id";
            return rootgetCommand.Execute().Id;
        }

        public void RecoverParents(IReadOnlyList<FileDTO> dirs)
        {
            var rootId = GetRootFolderId();

            foreach (var dir in dirs)
            {
                var getCommand = _driveService.Files.Get(dir.Id);
                getCommand.Fields = "id,parents";
                var originFile = getCommand.Execute();

                if (originFile.Parents == null || originFile.Parents.Count == 1 || !originFile.Parents.Contains(rootId))
                {
                    continue;
                }

                var updateCommand = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { }, originFile.Id);
                updateCommand.RemoveParents = rootId;
                updateCommand.Execute();
            }            
        }

        public void ReloadFromNewUser(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<int, FileDTO> callback)
        {
            var rootId = GetRootFolderId();

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var request = _driveService.Files.Get(file.Id);
                var stream = new MemoryStream();
                
                // download
                request.Download(stream);

                // upload
                stream.Seek(0, SeekOrigin.Begin);
                file.Parents = file.Parents?.Where(parent => parent != rootId).ToArray() ?? new string[] { };
                if (file.Parents.Count == 0)
                {
                    file.Parents = null;
                }
                newOwnerGoogleService.UploadFile(file, stream);

                // delete
                _driveService.Files.Delete(file.Id).Execute();

                callback(i, file);
            }
        }

        public void UploadFile(FileDTO file, Stream stream)
        {
            var a = _driveService.Files.Create(new Google.Apis.Drive.v3.Data.File
                {
                    Name = file.Name,
                    Parents = file.Parents
                }, stream, file.MimeType);
            a.Fields = "id";
            a.Upload();

            var needsToBeTrashed = file.ExplicitlyTrashed.HasValue && file.ExplicitlyTrashed.Value;
            if (!needsToBeTrashed)
            {
                return;
            }

            // only re-uploaded files needs to be explicitly trashed
            var loadedFileId = a.ResponseBody.Id;

            var updateCommand = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File
                {
                    Trashed = true
                }, loadedFileId);
            updateCommand.Execute();
        }
    }
}