using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThirdPartyPayloads
{
    public class SampleMessage
    {
        public int IntegerProperty { get; set; }
        public Boolean BooleanProperty { get; set; }

        public string StringProperty { get; set; }

        public SubMessage SomeSubClass { get; set; }
        public SampleMessage()
        {
        }
    }

    public class SubMessage
    {
        public int SomeIntValue { get; set; }
        public string SomStringValue { get; set; }
    }

}