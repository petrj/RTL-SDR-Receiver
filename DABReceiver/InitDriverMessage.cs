using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DABReceiver
{
    public class InitDriverMessage : ValueChangedMessage<string>
    {
        public InitDriverMessage() : base(String.Empty)
        {

        }
    }
}
