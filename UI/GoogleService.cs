using Google.Apis.Drive.v3;
using Google.Apis.Requests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

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
            listRequest.Fields = "nextPageToken,files(owners,ownedByMe,name,id,mimeType,parents,explicitlyTrashed)";
            var pageSize = 100;
            listRequest.PageSize = pageSize;

            var files = new List<Google.Apis.Drive.v3.Data.File>();

            do
            {
                var listResult = listRequest.Execute();
                var filesChunk = listResult.Files;
                listRequest.PageToken = listResult.NextPageToken;
                if (filesChunk == null || filesChunk.Count == 0)
                {
                    break;
                }

                files.AddRange(filesChunk);
                if (filesChunk.Count < pageSize)
                {
                    break;
                }

                Thread.Sleep(500);
            } while (true);

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

        public void DeleteOwnershipPermission(IReadOnlyList<FileDTO> files)
        {
            var batch = new BatchRequest(_driveService);

            BatchRequest.OnResponse<Google.Apis.Drive.v3.Data.Permission> batchCallback = delegate (
                Google.Apis.Drive.v3.Data.Permission permission,
                RequestError error,
                int index,
                System.Net.Http.HttpResponseMessage message)
            {
                
            };

            foreach(var file in files)
            {
                var deleteCommand = _driveService.Permissions.Delete(file.Id, file.OwnershipPermissionId);
                batch.Queue(deleteCommand, batchCallback);
            }

            batch.ExecuteAsync().Wait();
        }

        public IReadOnlyList<TransferingResult> TransferOwnershipTo(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<int, FileDTO> callback)
        {
            var newOwner = newOwnerGoogleService.GetUserInfo();

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

            var batch = new BatchRequest(_driveService);
            var result = new List<TransferingResult>();
            BatchRequest.OnResponse<Google.Apis.Drive.v3.Data.Permission> batchCallback = delegate (
                Google.Apis.Drive.v3.Data.Permission permission,
                RequestError error,
                int index,
                System.Net.Http.HttpResponseMessage message)
            {
                if (error != null)
                {
                    // Handle error
                    result.Add(new TransferingResult
                    {
                        Exception = new Exception(error.Message),
                        File = files[index]
                    });
                }
                else
                {
                    result.Add(new TransferingResult
                    {
                        File = files[index]
                    });
                    callback(index, files[index]);
                }
            };

            foreach (var commandDto in commandsDto)
            {
                batch.Queue(commandDto.command, batchCallback);

            }
            batch.ExecuteAsync().Wait();

            // removing edit permissions
            var filesToDeletePermission = result.Where(r => r.Success).Select(r => r.File).ToArray();
            newOwnerGoogleService.DeleteOwnershipPermission(filesToDeletePermission);

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
            var batch = new BatchRequest(_driveService);
            BatchRequest.OnResponse<object> batchCallback = delegate (
                object permission,
                RequestError error,
                int index,
                System.Net.Http.HttpResponseMessage message)
            {

            };

            var listRequest = _driveService.Files.List();
            listRequest.Fields = "files(id,parents)";
            var dirsData = listRequest.Execute().Files
                .Where(f => dirs.Any(dir => dir.Id == f.Id))
                .ToArray();

            foreach (var originFile in dirsData)
            {
                if (originFile.Parents == null || originFile.Parents.Count == 1 || !originFile.Parents.Contains(rootId))
                {
                    continue;
                }

                var updateCommand = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { }, originFile.Id);
                updateCommand.RemoveParents = rootId;
                batch.Queue(updateCommand, batchCallback);
            }

            batch.ExecuteAsync().Wait();
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