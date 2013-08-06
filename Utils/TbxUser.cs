using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Tbx.Utils;

namespace TeamboxLib.Utils
{
    /// <summary>
    /// Privilege level associated to a user.
    /// </summary>
    public enum UserPrivLevel
    {
        /// <summary>
        /// The user needs no special permissions.
        /// </summary>
        User,

        /// <summary>
        /// The user must be a workspace manager.
        /// </summary>
        Manager,

        /// <summary>
        /// The user must be a workspace administrator.
        /// </summary>
        Admin,

        /// <summary>
        /// The user must be a system administrator.
        /// </summary>
        Root
    }

    /// <summary>
    /// Represent a user in a workspace.
    /// 
    /// IMPORTANT: do NOT compare users by object pointers. Use the user ID
    /// do to so. The user objects can change dynamically.
    /// </summary>
    [Serializable]
    public class TbxUser : ISerializable
    {
        /// <summary>
        /// ID of the user.
        /// </summary>
        public UInt32 UserID = 0;

        /// <summary>
        /// Date at which the user was added.
        /// </summary>
        public UInt64 InvitationDate = 0;

        /// <summary>
        /// ID of the inviting user. 0 if none.
        /// </summary>
        public UInt32 InvitedBy = 0;

        /// <summary>
        /// Name given by the workspace administrator.
        /// </summary>
        public String AdminName = "";

        /// <summary>
        /// Name given by the user himself.
        /// </summary>
        public String UserName = "";

        /// <summary>
        /// Email address of the user, if any.
        /// </summary>
        public String EmailAddress = "";

        /// <summary>
        /// Organization name, if the user is a member.
        /// </summary>
        public String OrgName = "";

        /// <summary>
        /// Flags of the user.
        /// </summary>
        public UInt32 Flags = 0;

        /// <summary>
        /// True if the user is a virtual user. A virtual user is a user for 
        /// which no invitation event is associated. The root user is a virtual
        /// user. The KWM user is also a virtual user if its invitation event 
        /// was not yet processed.
        /// NonSerialized.
        /// </summary>
        public bool VirtualFlag = false;

        public bool AdminFlag
        {
            get { return (Flags & KAnpType.KANP_USER_FLAG_ADMIN) > 0; }
            set { SetFlagValue(KAnpType.KANP_USER_FLAG_ADMIN, value); }
        }

        public bool ManagerFlag
        {
            get { return (Flags & KAnpType.KANP_USER_FLAG_MANAGER) > 0; }
            set { SetFlagValue(KAnpType.KANP_USER_FLAG_MANAGER, value); }
        }

        public bool RegisterFlag
        {
            get { return (Flags & KAnpType.KANP_USER_FLAG_REGISTER) > 0; }
            set { SetFlagValue(KAnpType.KANP_USER_FLAG_REGISTER, value); }
        }

        public bool LockFlag
        {
            get { return (Flags & KAnpType.KANP_USER_FLAG_LOCK) > 0; }
            set { SetFlagValue(KAnpType.KANP_USER_FLAG_LOCK, value); }
        }

        public bool BanFlag
        {
            get { return (Flags & KAnpType.KANP_USER_FLAG_BAN) > 0; }
            set { SetFlagValue(KAnpType.KANP_USER_FLAG_BAN, value); }
        }

        /// <summary>
        /// Return the privilege level of the user.
        /// </summary>
        public UserPrivLevel PrivLevel
        {
            get
            {
                if (UserID == 0) return UserPrivLevel.Root;
                if (AdminFlag) return UserPrivLevel.Admin;
                if (ManagerFlag) return UserPrivLevel.Manager;
                return UserPrivLevel.User;
            }
        }

        /// <summary>
        /// Helper method to set or clear a user flag.
        /// </summary>
        private void SetFlagValue(UInt32 flag, bool value)
        {
            if (value) Flags |= flag;
            else Flags &= ~flag;
        }

        /// <summary>
        /// Get the username to display in the UI, without the user's email address (unless no
        /// username exists).
        /// </summary>
        public String UiSimpleName
        {
            get
            {
                if (AdminName != "") return AdminName;
                if (UserName != "") return UserName;
                return EmailAddress;
            }
        }

        /// <summary>
        /// Get the given name of the user (prénom). If no AdminName or UserName is
        /// set, use the left part of the email address.
        /// </summary>
        public String UiSimpleGivenName
        {
            get
            {
                // Give priority to AdminName.
                String name = AdminName != "" ? AdminName : UserName;

                // Try to get a valid given name.
                name = GetGivenName(name);

                if (name == "")
                    return GetEmailAddrLeftPart(EmailAddress);
                else
                    return name;
            }
        }

        /// <summary>
        /// Non-deserializing constructor.
        /// </summary>
        public TbxUser()
        {
        }

        /// <summary>
        /// Deserializing constructor.
        /// </summary>
        public TbxUser(SerializationInfo info, StreamingContext context)
        {
            Int32 version = Misc.GetSerializationVersion(info);

            UserID = info.GetUInt32("UserID");
            Misc.TryGetUInt64(info, ref InvitationDate, "InvitationDate", 0);
            Misc.TryGetUInt32(info, ref InvitedBy, "InvitedBy", 0);
            AdminName = info.GetString("AdminName");
            UserName = info.GetString("UserName");
            EmailAddress = info.GetString("EmailAddress");
            OrgName = info.GetString("OrgName");

            if (version < 7)
            {
                if (info.GetUInt32("Power") > 0)
                {
                    AdminFlag = true;
                    ManagerFlag = true;
                }

                if (UserName != "")
                {
                    RegisterFlag = true;
                }
            }

            if (version >= 7)
            {
                Flags = info.GetUInt32("Flags");
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Misc.AddSerializationVersion(info);
            info.AddValue("UserID", UserID);
            info.AddValue("InvitationDate", InvitationDate);
            info.AddValue("InvitedBy", InvitedBy);
            info.AddValue("AdminName", AdminName);
            info.AddValue("UserName", UserName);
            info.AddValue("EmailAddress", EmailAddress);
            info.AddValue("OrgName", OrgName);
            info.AddValue("Flags", Flags);
        }

        /// <summary>
        /// Return the first characters of the given name until a space
        /// is found.
        /// </summary>
        private String GetGivenName(String name)
        {
            String[] splitted = name.Split(new char[] { ' ' });
            if (splitted.Length > 0) return splitted[0];
            else return "";
        }

        /// <summary>
        /// Return the left part of an email address, the entire address
        /// if any problem occurs.
        /// </summary>
        private String GetEmailAddrLeftPart(String addr)
        {
            String[] splitted = addr.Split(new char[] { '@' });
            if (splitted.Length > 0) return splitted[0];
            else return addr;
        }

        /// <summary>
        /// Get the username to display in the UI, with its email address appended. If no
        /// username is present, return the email address only.
        /// </summary>
        public String UiFullName
        {
            get
            {
                if (AdminName == "" && UserName == "") return EmailAddress;
                if (UiSimpleName == EmailAddress) return EmailAddress;
                return UiSimpleName + " (" + EmailAddress + ")";
            }
        }

        /// <summary>
        /// Get the KwsUser description text.
        /// </summary>
        public String UiTooltipText
        {
            get
            {
                if (UiSimpleName == EmailAddress) return EmailAddress;
                return UiSimpleName + Environment.NewLine + EmailAddress;
            }
        }

        /// <summary>
        /// Return true if the user has an administrative name set.
        /// </summary>
        public bool HasAdminName()
        {
            return AdminName != "";
        }
    }
}
