using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class NotifyDriverIconChangeMessage : ValueChangedMessage<object>
    {
        public NotifyDriverIconChangeMessage() : base(null)
        {
        }
    }
}
