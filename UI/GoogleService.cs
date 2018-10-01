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

            listRequest.Fields = "files(owners,ownedByMe,name,id,mimeType)";
            var files = listRequest.Execute().Files;

            var currentUser = GetUserInfo();

            return files
                .Where(file => file.OwnedByMe.HasValue && file.OwnedByMe.Value)
                .Select(f => new FileDTO
                {
                    Id = f.Id,
                    Name = f.Name,
                    OwnershipPermissionId = f.Owners.First(owner => owner.EmailAddress == currentUser.EmailAddress).PermissionId,
                    MimeType = f.MimeType
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

                // removing view and edit permissions of an old owner
                var file = commandDto.file;
                newOwnerGoogleService.DeleteOwnershipPermission(file);

                result.Add(new TransferingResult
                {
                    File = commandDto.file
                });
            }

            return result;
        }

        public void ReloadFromNewUser(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<int, FileDTO> callback)
        {
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var request = _driveService.Files.Get(file.Id);
                var stream = new MemoryStream();
                
                // download
                request.Download(stream);

                // upload
                stream.Seek(0, SeekOrigin.Begin);
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
                Name = file.Name
            }, stream, file.MimeType);

            a.Upload();
        }
    }
}