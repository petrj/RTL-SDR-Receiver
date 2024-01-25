using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 *    Copyright (C) 2018
 *    Matthias P. Braendli (matthias.braendli@mpb.li)
 *
 *    Copyright (C) 2013
 *    Jan van Katwijk (J.vanKatwijk@gmail.com)
 *    Lazy Chair Programming
 *
 *    This file is part of the SDR-J (JSDR).
 *    SDR-J is free software; you can redistribute it and/or modify
 *    it under the terms of the GNU General Public License as published by
 *    the Free Software Foundation; either version 2 of the License, or
 *    (at your option) any later version.
 *
 *    SDR-J is distributed in the hope that it will be useful,
 *    but WITHOUT ANY WARRANTY; without even the implied warranty of
 *    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *    GNU General Public License for more details.
 *
 *    You should have received a copy of the GNU General Public License
 *    along with SDR-J; if not, write to the Free Software
 *    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 *
 */

namespace DAB
{
    public class EnergyDispersal
    {
        private byte[] _dispersalVector;

        public byte[] Dedisperse(byte[] data)
        {
            if (_dispersalVector == null || _dispersalVector.Length != data.Length)
            {
                var shiftRegister = new byte[9] { 1, 1, 1, 1, 1, 1, 1, 1, 1 };

                _dispersalVector = new byte[data.Length];

                for (var i = 0; i < data.Length; i++)
                {
                    var b = Convert.ToByte(shiftRegister[8] ^ shiftRegister[4]);
                    for (int j = 8; j > 0; j--)
                    {
                        shiftRegister[j] = shiftRegister[j - 1];
                    }

                    shiftRegister[0] = b;
                    _dispersalVector[i] ^= b;
                }
            }

            for (var i = 0; i < data.Length; i++)
            {
                data[i] ^= _dispersalVector[i];
            }

            return data;
        }
    }
}
