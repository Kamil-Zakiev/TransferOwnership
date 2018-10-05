using Google.Apis.Drive.v3;
using Google.Apis.Requests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace UI
{
    /// <summary>
    /// Adapter to a Google.Apis.Drive.v3.DriveService
    /// </summary>
    public class GoogleService : IGoogleService
    {
        private DriveService _driveService;

        // see https://support.google.com/drive/answer/2494892
        private readonly IReadOnlyList<string> _googleAppTypes = new[] {
            "application/vnd.google-apps.document",
            "application/vnd.google-apps.spreadsheet",
            "application/vnd.google-apps.presentation",
            "application/vnd.google-apps.form",
            "application/vnd.google-apps.folder",
            "application/vnd.google-apps.map",
            "application/vnd.google-apps.site",
            "application/vnd.google-apps.drawing"
        };

        public GoogleService(DriveService driveService)
        {
            _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        }

        private void Timeout(int seconds = 5)
        {
            Thread.Sleep(200);
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

                Timeout(1);
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

        private UserInfo _userInfo;
        public UserInfo GetUserInfo()
        {
            if (_userInfo != null)
            {
                return _userInfo;
            }

            const string defaultUserUrl = "https://icon-icons.com/icons2/1378/PNG/512/avatardefault_92824.png";
            var aboutGet = _driveService.About.Get();
            aboutGet.Fields = "user";

            var user = aboutGet.Execute().User;
            return _userInfo = new UserInfo
            {
                EmailAddress = user.EmailAddress,
                Name = user.DisplayName,
                PhotoLink = user.PhotoLink != null ? new Uri(user.PhotoLink) : new Uri(defaultUserUrl)
            };
        }

        public void DeleteOwnershipPermission(IReadOnlyList<FileDTO> files)
        {
            var batch = new BatchRequest(_driveService);

            foreach(var file in files)
            {
                var deleteCommand = _driveService.Permissions.Delete(file.Id, file.OwnershipPermissionId);
                batch.Queue<object>(deleteCommand, EmbtyBatchCallback);
            }

            batch.ExecuteAsync().Wait();
            Timeout();
        }

        public void RejectRights(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<FileDTO> callback)
        {
            var googleFiles = files.Where(file => _googleAppTypes.Contains(file.MimeType)).ToArray();
            TransferOwnershipTo(googleFiles, newOwnerGoogleService, callback);

            var loadedFiles = files.Except(googleFiles).ToArray();
            ReloadFromNewUser(loadedFiles, newOwnerGoogleService, callback);
        }

        private void TransferOwnershipTo(IReadOnlyList<FileDTO> googleFiles, IGoogleService newOwnerGoogleService, Action<FileDTO> callback)
        {
            var newOwner = newOwnerGoogleService.GetUserInfo();

            var commandsDto = googleFiles
                .Select(file =>
                {
                    var command = _driveService.Permissions.Create(new Google.Apis.Drive.v3.Data.Permission
                    {
                        Role = "owner",
                        Type = "user",
                        EmailAddress = newOwner.EmailAddress
                    }, file.Id);

                    command.TransferOwnership = true;
                    return new { command, file };
                })
                .ToArray();

            BatchRequest.OnResponse<Google.Apis.Drive.v3.Data.Permission> batchCallback = delegate (
                Google.Apis.Drive.v3.Data.Permission permission,
                RequestError error,
                int index,
                System.Net.Http.HttpResponseMessage message)
            {
                if (!string.IsNullOrWhiteSpace(error?.Message))
                {
                    throw new InvalidOperationException(error.Message);
                }

                callback(googleFiles[index]);
            };

            var batch = new BatchRequest(_driveService);
            foreach (var commandDto in commandsDto)
            {
                batch.Queue(commandDto.command, batchCallback);
            }
            batch.ExecuteAsync().Wait();

            // removing edit permissions
            Thread.Sleep(2000);
            newOwnerGoogleService.DeleteOwnershipPermission(googleFiles);

            // correct dirs chain
            Thread.Sleep(2000);
            newOwnerGoogleService.RecoverParents(googleFiles);
        }

        private string _rootId;
        private string GetRootFolderId()
        {
            if (_rootId != null)
            {
                return _rootId;
            }

            var rootgetCommand = _driveService.Files.Get("root");
            rootgetCommand.Fields = "id";
            _rootId = rootgetCommand.Execute().Id;

            Timeout();
            return _rootId;
        }

        public void RecoverParents(IReadOnlyList<FileDTO> files)
        {
            var rootId = GetRootFolderId();
            var batch = new BatchRequest(_driveService);

            var listRequest = _driveService.Files.List();
            listRequest.Fields = "files(id,parents)";
            var filesData = listRequest.Execute().Files
                .Where(f => files.Any(dir => dir.Id == f.Id))
                .ToArray();
            Timeout();

            foreach (var originFile in filesData)
            {
                if (originFile.Parents == null || originFile.Parents.Count == 1 || !originFile.Parents.Contains(rootId))
                {
                    continue;
                }

                var updateCommand = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { }, originFile.Id);
                updateCommand.RemoveParents = rootId;
                batch.Queue<object>(updateCommand, EmbtyBatchCallback);
            }

            batch.ExecuteAsync().Wait();
            Timeout();
        }

        private void ReloadFromNewUser(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<FileDTO> callback)
        {
            var rootId = GetRootFolderId();

            var filesToBeTrashed = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var request = _driveService.Files.Get(file.Id);
                var stream = new MemoryStream();
                
                // download
                request.Download(stream);
                Timeout();

                // upload
                stream.Seek(0, SeekOrigin.Begin);
                file.Parents = file.Parents?.Where(parent => parent != rootId).ToArray() ?? new string[] { };
                if (file.Parents.Count == 0)
                {
                    file.Parents = null;
                }

                var loadedFileId = newOwnerGoogleService.UploadFile(file, stream);
                if (file.ExplicitlyTrashed.HasValue && file.ExplicitlyTrashed.Value)
                {
                    filesToBeTrashed.Add(loadedFileId);
                }

                callback(file);
            }

            // delete
            var batch = new BatchRequest(_driveService);
            foreach (var file in files)
            {
                var deleteCommand = _driveService.Files.Delete(file.Id);
                batch.Queue<object>(deleteCommand, EmbtyBatchCallback);
            }

            batch.ExecuteAsync().Wait();
            Timeout();

            newOwnerGoogleService.TrashFiles(filesToBeTrashed);
        }

        private void EmbtyBatchCallback(
                object permission,
                RequestError error,
                int index,
                System.Net.Http.HttpResponseMessage message)
        {
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                throw new InvalidOperationException(error.Message);
            }
        }

        public void TrashFiles(IReadOnlyList<string> filesIdsToTrash)
        {
            var batch = new BatchRequest(_driveService);
            foreach (var fileId in filesIdsToTrash)
            {
                var updateCommand = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File
                {
                    Trashed = true
                }, fileId);
                batch.Queue<object>(updateCommand, EmbtyBatchCallback);
            }

            batch.ExecuteAsync().Wait();
            Timeout();
        }

        public string UploadFile(FileDTO file, Stream stream)
        {
            var createFileCommand = _driveService.Files.Create(new Google.Apis.Drive.v3.Data.File
                {
                    Name = file.Name,
                    Parents = file.Parents
                }, stream, file.MimeType);
            createFileCommand.Fields = "id";
            createFileCommand.Upload();
            Timeout();

            return createFileCommand.ResponseBody.Id;
        }
    }
}