using System;
using System.Linq;

namespace ROBdk97.XmlDocToMd
{
    /// <summary>
    /// Helper internal class for the Node conversion
    /// </summary>
    internal class ConversionContext
    {
        /// <summary>
        /// Warning log writer.  Throws exceptions if null.
        /// </summary>
        internal IWarningLogger WarningLogger { get; set; }
        internal string AssemblyName { get; set; }
        internal ConversionContext MutateAssemblyName(string assemblyName)
        {
            AssemblyName = assemblyName;
            return this;
        }
        internal UnexpectedTagActionEnum UnexpectedTagAction { get; set; } = UnexpectedTagActionEnum.Error;
    }
}
