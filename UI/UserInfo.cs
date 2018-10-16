﻿using System;

namespace UI
{
    /// <summary>
    /// User's DTO
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// Owners email
        /// </summary>
        public string EmailAddress { get; set; }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Uri to a photo link
        /// </summary>
        public Uri PhotoLink { get; set; }

        public string RootFolderId { get; set; }
    }
}
