using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class DriverInitializationFailedMessage : ValueChangedMessage<object>
    {
        public DriverInitializationFailedMessage(object value) : base(value)
        {

        }
    }
}
