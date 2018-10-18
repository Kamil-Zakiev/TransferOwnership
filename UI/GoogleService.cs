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

        private readonly IExpBackoffPolicy _expBackoffPolicy;
        private readonly ILogger _logger;

        public GoogleService(DriveService driveService, IExpBackoffPolicy expBackoffPolicy, ILogger logger)
        {
            _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
            _expBackoffPolicy = expBackoffPolicy ?? throw new ArgumentNullException(nameof(expBackoffPolicy));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<FileDTO> GetOwnedFiles()
        {
            var currentUser = GetUserInfo();

            var listRequest = _driveService.Files.List();
            listRequest.Q = $"'{currentUser.EmailAddress}' in owners";
            listRequest.Fields = "nextPageToken,files(owners,ownedByMe,name,id,mimeType,parents,explicitlyTrashed,permissions)";
            var pageSize = 100;
            listRequest.PageSize = pageSize;

            var files = new List<Google.Apis.Drive.v3.Data.File>();

            do
            {
                _logger.LogMessage($"Получаем файлы пользователя '{currentUser.EmailAddress}' с использованием токена c хэшем = '{listRequest.PageToken?.GetHashCode().ToString() ?? "<null>"}'.");
                var listResult = listRequest.Execute();
                var filesChunk = listResult.Files;
                listRequest.PageToken = listResult.NextPageToken;
                if (filesChunk == null || filesChunk.Count == 0)
                {
                    break;
                }

                files.AddRange(filesChunk);
                if (filesChunk.Count < pageSize || listRequest.PageToken == null)
                {
                    break;
                }

            } while (true);

            _logger.LogMessage($"Файлы пользователя '{currentUser.EmailAddress}' получены.");

            return files
                .Where(file => file.OwnedByMe.HasValue && file.OwnedByMe.Value)
                .Select(f => new FileDTO
                {
                    Id = f.Id,
                    Name = f.Name,
                    OwnershipPermissionId = f.Owners.First(owner => owner.EmailAddress == currentUser.EmailAddress).PermissionId,
                    MimeType = f.MimeType,
                    Parents = f.Parents,
                    ExplicitlyTrashed = f.ExplicitlyTrashed,
                    Permissions = f.Permissions
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

            _logger.LogMessage("Получаем данные о пользователе.");

            const string defaultUserUrl = "https://icon-icons.com/icons2/1378/PNG/512/avatardefault_92824.png";
            var aboutGet = _driveService.About.Get();
            aboutGet.Fields = "user";

            var user = aboutGet.Execute().User;

            _logger.LogMessage($"Данные о пользователе '{user.EmailAddress}' получены.");

            _logger.LogMessage("Получаем данные о корневой папке.");
            var rootgetCommand = _driveService.Files.Get("root");
            rootgetCommand.Fields = "id";
            var rootId = rootgetCommand.Execute().Id;

            _logger.LogMessage("Данные о корневой папке получены.");

            return _userInfo = new UserInfo
            {
                EmailAddress = user.EmailAddress,
                Name = user.DisplayName,
                PhotoLink = user.PhotoLink != null ? new Uri(user.PhotoLink) : new Uri(defaultUserUrl),
                RootFolderId = rootId
            };
        }

        public void DeleteOwnershipPermission(IReadOnlyList<FileDTO> files)
        {
            _logger.LogMessage("Запускаем функцию удаления прав старого пользователя в пакетном режиме.");
            WrapBatchOperation(files, file => _driveService.Permissions.Delete(file.Id, file.OwnershipPermissionId));

            _logger.LogMessage("Функция удаления прав старого пользователя завершила свою работу.");
        }

        public void RejectRights(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<FileDTO> callback)
        {
            _logger.LogMessage("Запускаем функцию переноса гугл-документов.");
            var googleFiles = files.Where(file => _googleAppTypes.Contains(file.MimeType)).ToArray();
            TransferOwnershipTo(googleFiles, newOwnerGoogleService, callback);
            _logger.LogMessage("Переноса гугл-документов завершен.");

            _logger.LogMessage("Запускаем функцию переноса обычных файлов.");
            var loadedFiles = files.Except(googleFiles).ToArray();
            ReloadFromNewUser(loadedFiles, newOwnerGoogleService, callback);
            _logger.LogMessage("Перенос обычных файлов завершен.");
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
                    command.EmailMessage = "Автоматический перенос.";
                    return new { command, file };
                })
                .ToArray();

            _logger.LogMessage("Запускаем функцию переноса гугл-документов в пакетном режиме.");
            WrapBatchOperation(commandsDto, commandDto => commandDto.command, (index) => callback(googleFiles[index]));
            
            // removing edit permissions
            _logger.LogMessage("Останавливаем поток на 2 секунды, чтобы все данные сохранились в сервисах Google Drive");
            Thread.Sleep(2000);
            newOwnerGoogleService.DeleteOwnershipPermission(googleFiles);

            // correct dirs chain
            _logger.LogMessage("Останавливаем поток на 2 секунды, чтобы все данные сохранились в сервисах Google Drive");
            Thread.Sleep(2000);
            newOwnerGoogleService.RecoverParents(googleFiles);
        }
        
        public void RecoverParents(IReadOnlyList<FileDTO> files)
        {
            _logger.LogMessage("Восстанавливаем иерархию для гугл-документов.");
            var rootId = GetUserInfo().RootFolderId;

            var filesData = GetOwnedFiles();

            _logger.LogMessage("Восстанавливаем иерархию для гугл-документов в пакетном режиме.");
            WrapBatchOperation(filesData, fileData => 
            {
                if (fileData.Parents == null || fileData.Parents.Count == 1 || !fileData.Parents.Contains(rootId))
                {
                    return null;
                }

                var updateCommand = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { }, fileData.Id);
                updateCommand.RemoveParents = rootId;
                return updateCommand;
            });

            _logger.LogMessage("Восстанавливание иерархии для гугл-документов закончено.");
        }

        private void ReloadFromNewUser(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService, Action<FileDTO> callback)
        {
            _logger.LogMessage("Перезагружаем файлы от имени нового пользователя.");
            var rootId = GetUserInfo().RootFolderId;

            var filesToBeTrashed = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var request = _driveService.Files.Get(file.Id);
                var stream = new MemoryStream();

                // download
                _logger.LogMessage($"Скачиваем файл \"{file.Name}\" ({file.Id}).");
                _expBackoffPolicy.GrantedDelivery(() => request.Download(stream));

                // upload
                _logger.LogMessage($"Загружаем файл {file.Name}.");
                stream.Seek(0, SeekOrigin.Begin);
                file.Parents = file.Parents?.Where(parent => parent != rootId).ToArray() ?? new string[] { };
                if (file.Parents.Count == 0)
                {
                    file.Parents = null;
                }

                _expBackoffPolicy.GrantedDelivery(() =>
                {
                    var loadedFileId = newOwnerGoogleService.UploadFile(file, stream);
                    if (file.ExplicitlyTrashed.HasValue && file.ExplicitlyTrashed.Value && !string.IsNullOrWhiteSpace(loadedFileId))
                    {
                        filesToBeTrashed.Add(loadedFileId);
                    }

                    callback(file);
                });
                _logger.LogMessage($"Загрузили файл \"{file.Name}\".");
            }

            // delete
            _logger.LogMessage("Удаляем файлы старого пользователя в пакетном режиме.");
            WrapBatchOperation(files, file => _driveService.Files.Delete(file.Id));

            newOwnerGoogleService.TrashFiles(filesToBeTrashed);
            _logger.LogMessage("Файлы старого пользователя удалены.");
        }

        private void WrapBatchOperation<TSource>(IReadOnlyList<TSource> sourceList, Func<TSource, IClientServiceRequest> commandProvider, Action<int> callback = null)
        {
            _logger.LogMessage("Начало пакетной операции.");
            // nonlinear logic!
            List<RequestsInfo> requestsInfo = null;
            BatchRequest.OnResponse<object> batchCallback = (_, error, index, __) =>
            {
                requestsInfo[index].ErrorMsg = error?.Message;
                if (requestsInfo[index].Success)
                {
                    callback?.Invoke(index);
                }

                if (index == (requestsInfo.Count-1) && requestsInfo.Any(ri => !ri.Success))
                {
                    var messages = requestsInfo.Where(ri => !ri.Success).Select(ri => ri.ErrorMsg).Distinct();
                    throw new InvalidOperationException(string.Join(Environment.NewLine, messages));
                }
            };

            var consideredCount = 0;
            const int batchSize = 100;
            while (consideredCount != sourceList.Count)
            {
                var batchSources = sourceList.Skip(consideredCount).Take(batchSize).ToArray();

                var batch = new BatchRequest(_driveService);
                requestsInfo = new List<RequestsInfo>();
                foreach (var source in batchSources)
                {
                    var request = commandProvider(source);
                    if (request == null)
                    {
                        continue;
                    }

                    requestsInfo.Add(new RequestsInfo
                    {
                        Request = request
                    });
                    batch.Queue(request, batchCallback);
                }

                _logger.LogMessage($"Сформирован пакет запросов из {batch.Count} штук.");
                _expBackoffPolicy.GrantedDelivery(
                    () =>
                    {
                        _logger.LogMessage($"Отправка пакета на сервер Google Drive...");
                        batch.ExecuteAsync().Wait();
                        _logger.LogMessage($"Отправка пакета на сервер Google Drive прошла успешно.");
                    }, 
                    () =>
                    {
                        _logger.LogMessage($"Из-за ошибки при пакетной операции, будет сформирован новый пакет из провальных запросов");
                        // prepare retry batch composed of nonsuccess requests
                        requestsInfo = requestsInfo.Where(ri => !ri.Success).ToList();
                        var newBatch = new BatchRequest(_driveService);
                        foreach (var requestInfo in requestsInfo)
                        {
                            newBatch.Queue(requestInfo.Request, batchCallback);
                        }
                        _logger.LogMessage($"Сформирован новый пакет запросов из {newBatch.Count} штук...");
                        newBatch.ExecuteAsync().Wait();
                        _logger.LogMessage($"Отправка пакета на сервер Google Drive прошла успешно.");
                    });
                consideredCount += batchSources.Length;
            }
            _logger.LogMessage("Пакетная операция завершена.");
        }

        class RequestsInfo
        {
            public IClientServiceRequest Request { get; set; }
            public bool Success => string.IsNullOrWhiteSpace(ErrorMsg);
            public string ErrorMsg { get; set; }
        }

        public void TrashFiles(IReadOnlyList<string> filesIdsToTrash)
        {
            _logger.LogMessage($"Помещение в корзину негугловских файлов в пакетном режиме.");
            WrapBatchOperation(filesIdsToTrash, fileId => _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File
            {
                Trashed = true
            }, fileId));
        }

        public string UploadFile(FileDTO file, Stream stream)
        {
            _logger.LogMessage($"Загрузка файла \"{file.Name}\" на сервер Google Drive в одиночном режиме.");
            var createFileCommand = _driveService.Files.Create(new Google.Apis.Drive.v3.Data.File
                {
                    Name = file.Name,
                    Parents = file.Parents
                }, stream, file.MimeType);
            createFileCommand.Fields = "id";
            _expBackoffPolicy.GrantedDelivery(() => createFileCommand.Upload());
            _logger.LogMessage($"Загрузка файла \"{file.Name}\" завершена.");

            return createFileCommand.ResponseBody?.Id;
        }
    }
}