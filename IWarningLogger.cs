using System;
using System.IO;
using System.Linq;

namespace ROBdk97.XmlDocToMd
{
    internal interface IWarningLogger
    {
        void LogWarning(string warning);
    }

    internal class TextWriterWarningLogger : IWarningLogger
    {
        private TextWriter _textWriter;
        internal TextWriterWarningLogger(TextWriter textWriter)
        {
            _textWriter = textWriter;
        }
        public void LogWarning(string warning)
        {
            _textWriter.WriteLine("WARN: " + warning);
        }
    }
}
