using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class InitTCPDriverMessage : ValueChangedMessage<object>
    {
        public InitTCPDriverMessage(object settings) : base(settings)
        {

        }
    }
}
