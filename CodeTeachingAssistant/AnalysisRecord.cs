using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeTeachingAssistant
{
    internal class AnalysisRecord
    {
        public string Time { get; set; }
        public string Output { get; set; }
        public bool IsSyntaxClean { get; set; }
        public List<string> Errors { get; set; }
    }
}
