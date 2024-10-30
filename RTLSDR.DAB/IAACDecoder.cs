using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDR.DAB
{
    public interface IAACDecoder
    {
        bool Init(AACSuperFrameHeader format);
        byte[] DecodeAAC(byte[] aacData);
        void Close();
    }
}
