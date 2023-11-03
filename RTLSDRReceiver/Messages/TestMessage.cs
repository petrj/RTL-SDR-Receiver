using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class TestMessage : ValueChangedMessage<object>
    {
        public TestMessage() : base(null)
        {

        }
    }
}
