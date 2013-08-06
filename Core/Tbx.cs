using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TeamboxLib.Core
{
    /// <summary>
    /// Represent the run level of a workspace.
    /// </summary>
    public enum TbxWsRunLevel
    {
        /// <summary>
        /// The workspace isn't ready to work offline.
        /// </summary>
        Stopped,

        /// <summary>
        /// The workspace is ready to work offline.
        /// </summary>
        Offline,

        /// <summary>
        /// The workspace is ready to work online.
        /// </summary>
        Online
    }

    public class Tbx
    {
    }
}
