using Google.Apis.Drive.v3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            listRequest.Fields = "files(owners,ownedByMe,name,id)";
            var files = listRequest.Execute().Files;

            var currentUser = GetUserInfo();

            return files
                .Where(file => file.OwnedByMe.HasValue && file.OwnedByMe.Value)
                .Select(f => new FileDTO
                {
                    Id = f.Id,
                    Name = f.Name,
                    OwnershipPermissionId = f.Owners.First(owner => owner.EmailAddress == currentUser.EmailAddress).PermissionId
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

        public async Task DeleteOwnershipPermission(FileDTO file)
        {
            var deleteCommand = _driveService.Permissions.Delete(file.Id, file.OwnershipPermissionId);
            await deleteCommand.ExecuteAsync();
        }

        public async Task TransferOwnershipTo(IReadOnlyList<FileDTO> files, IGoogleService newOwnerGoogleService)
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

            foreach (var commandDto in commandsDto)
            {
                // introducing new owner of a file
                var command = commandDto.command;
                await command.ExecuteAsync();

                // removing view and edit permissions of an old owner
                var file = commandDto.file;
                await newOwnerGoogleService.DeleteOwnershipPermission(file);
            }
        }
    }
}