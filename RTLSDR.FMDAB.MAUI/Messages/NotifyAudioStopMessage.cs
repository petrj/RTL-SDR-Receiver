using CommunityToolkit.Mvvm.Messaging.Messages;
using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class NotifyAudioStopMessage : ValueChangedMessage<object>
    {
        public NotifyAudioStopMessage() : base(null)
        {

        }
    }
}
