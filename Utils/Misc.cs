using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml;
using Tbx.Utils;

namespace TeamboxLib.Utils
{
    /// <summary>
    /// This class contains various static utility methods that can be useful 
    /// throughout the code.
    /// </summary>
    public class Misc
    {
        /// <summary>
        /// Current serialization version of the KWM.
        /// </summary>
        public const Int32 SerializationVersion = 7;

        // Singleton.
        private Misc()
        {
        }

        public static String KwmVersion
        {
            get
            {
                return "2.0";
            }
        }

        /// <summary>
        /// Return the serialization version extracted from the
        /// SerializationInfo object specified. For compatibility purposes
        /// we use the name 'version' in lower case for storing the version
        /// number. If the version field is not present, the value 0 is
        /// returned.
        /// </summary>
        public static Int32 GetSerializationVersion(SerializationInfo info)
        {
            try
            {
                return info.GetInt32("version");
            }

            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Add the serialization version in the SerializationInfo object
        /// specified. 
        /// </summary>
        public static void AddSerializationVersion(SerializationInfo info)
        {
            info.AddValue("version", SerializationVersion);
        }

        public static String GetVncServerRegKey()
        {
            return Base.GetKwmRegKeyString() + "\\vnc";
        }

        public static String GetKwmLogFilePath()
        {
            return Base.GetKcsLogFilePath() + "kwm\\";
        }

        public static String GetKmodDirPath()
        {
            return Base.GetKcsLogFilePath() + "kmod";
        }

        public static String GetKtlstunnelLogFilePath()
        {
            return Base.GetKcsLogFilePath() + "ktlstunnel\\";
        }

        public static String GetKwmRoamingStatePath()
        {
            return Base.GetKcsRoamingDataPath() + "kwm\\state\\";
        }

        public static String GetCorruptedWmPath()
        {
            return Base.GetKcsRoamingDataPath() + "kwm\\state\\Corrupted\\";
        }

        public static String GetKwmDbPath()
        {
            return GetKwmRoamingStatePath() + "local.db";
        }

        public static String GetKfsDefaultStorePath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\Teambox Shares\\";
        }

        /// <summary>
        /// Don't ask, don't tell. Close your eyes. Go away. Life is too short
        /// to dick around with this crap.
        /// </summary>
        private static String EscapeArgForFatalError(String insanity)
        {
            insanity = insanity.Replace("\"", "\"\"");
            insanity = insanity.Replace("\\", "\"\\\\\"");
            return insanity;
        }

        /// <summary>
        /// Try to get the desired serialized String element. If not present, use
        /// an empty String.
        /// </summary>
        public static void TryGetString(SerializationInfo info, ref String var, String name)
        {
            try
            {
                var = "";
                var = info.GetString(name);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Try to get the desired serialized UInt32 element. If not present, use
        /// defVal instead.
        /// </summary>
        public static void TryGetUInt32(SerializationInfo info, ref UInt32 var, String name, UInt32 defVal)
        {
            try
            {
                var = defVal;
                var = info.GetUInt32(name);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Try to get the desired serialized UInt64 element. If not present, use
        /// defVal instead.
        /// </summary>
        public static void TryGetUInt64(SerializationInfo info, ref UInt64 var, String name, UInt64 defVal)
        {
            try
            {
                var = defVal;
                var = info.GetUInt64(name);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Return the displayable name of a given application. 
        /// KAnp 'type' field must be masked with KAnpType.NAMESPACE_ID_MASK in
        /// order to get the right application ID.
        /// </summary>
        public static string GetApplicationName(UInt32 application)
        {
            switch (application)
            {
                case KAnpType.KANP_NS_CHAT: return "Message board";
                case KAnpType.KANP_NS_KFS: return "Files";
                case KAnpType.KANP_NS_VNC: return "Screen sharing";
                case KAnpType.KANP_NS_WB: return "Whiteboard";
                case KAnpType.KANP_NS_PB: return "Attachment Management";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Create a new XmlElement and add it to the specified XmlElement if parent is not null.
        /// </summary>
        public static XmlElement CreateXmlElement(XmlDocument doc, XmlElement parent, String name, String text)
        {
            XmlElement elem = doc.CreateElement(name);
            if (text != "")
            {
                XmlText txtElem = doc.CreateTextNode(text);
                elem.AppendChild(txtElem);
            }

            if (parent != null)
                parent.AppendChild(elem);

            return elem;
        }

        /// <summary>
        /// Return the child element 'name' in 'parent', if any.
        /// </summary>
        public static XmlElement GetXmlChildElement(XmlElement parent, String name)
        {
            XmlNodeList list = parent.GetElementsByTagName(name);
            return (list.Count == 0) ? null : list.Item(0) as XmlElement;
        }

        /// <summary>
        /// Return the value associated to the child element 'name' in
        /// 'parent'. The string 'defaultText' is returned if the child
        /// element is not found.
        /// </summary>
        public static String GetXmlChildValue(XmlElement parent, String name, String defaultText)
        {
            XmlElement elem = GetXmlChildElement(parent, name);
            return (elem == null) ? defaultText : elem.InnerText;
        }
    }
}
