using System;
namespace DAB
{
    public abstract class MSCDescription
    {
        public bool Primary { get; set; }

        public bool AccessControl { get; set; }
        // false: no access control or access control applies only to a part of the service component;
        // true: access control applies to the whole of the service component.
    }
}
