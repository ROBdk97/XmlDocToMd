using System;
using System.Linq;

namespace ROBdk97.XmlDocToMd
{
    /// <summary>
    /// Specifies the manner in which unexpected tags will be handled
    /// </summary>
    public enum UnexpectedTagActionEnum
    {
        /// <summary>
        /// No unexpected tags are allowed
        /// </summary>
        Error,
        /// <summary>
        /// Warn on unexpected tags
        /// </summary>
        Warn,
        /// <summary>
        /// All unexpected tags are allowed
        /// </summary>
        Accept
    }
}
