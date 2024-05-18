﻿using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class ToastMessage : ValueChangedMessage<string>
    {
        public ToastMessage(string value) : base(value)
        {

        }
    }
}
