using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class DisconnectDriverMessage : ValueChangedMessage<object>
    {
        public DisconnectDriverMessage() : base(null)
        {

        }
    }
}
