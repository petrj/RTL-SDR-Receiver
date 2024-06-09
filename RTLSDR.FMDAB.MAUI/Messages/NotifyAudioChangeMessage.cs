using CommunityToolkit.Mvvm.Messaging.Messages;
using RTLSDR.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class NotifyAudioChangeMessage : ValueChangedMessage<object>
    {
        public NotifyAudioChangeMessage(AudioDataDescription audioDescription) : base(audioDescription)
        {

        }
    }
}
