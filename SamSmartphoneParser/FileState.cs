using System;
using System.Collections.Generic;

namespace SmartphoneParserReport
{
    public class FileState
    {
        public long LastPosition;
        public List<string> Buffer = new List<string>();
        public DateTime LastUpdate = DateTime.Now;
        public string FileName;
    }
}
