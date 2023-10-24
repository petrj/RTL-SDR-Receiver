﻿using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DABReceiver
{
    public class DriverInitializedMessage : ValueChangedMessage<object>
    {
        public DriverInitializedMessage(object value) : base(value)
        {

        }
    }
}
