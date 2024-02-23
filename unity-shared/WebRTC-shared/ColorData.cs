using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class ColorData
    {
        private byte[] ColorArray { get; set; } 
        public static uint Width { get; private set; }
        public static uint Height { get; private set; }

        public Queue<byte[]> ColorPackets {get; private set; }
        
        public void InitColorFrame(uint width, uint height)
        {
            Width = width;
            Height = height;
            ColorArray = new byte[Width * Height * 4];
            ColorPackets = new Queue<byte[]>();
        }

        public void SetPixelBinary(int pixel, bool value)
        {
            byte b;
            if (value == false)
            {
                b = 0x00;
            }
            else
            {
                b = 0xff;
            }
            // [0]...B, [1]...G, [2]..R, [3]..A
            ColorArray[pixel * 4 + 0] = b;  // blue
            ColorArray[pixel * 4 + 1] = b; // green
            ColorArray[pixel * 4 + 2] = b;  // red
            ColorArray[pixel * 4 + 3] = 0xFF;  // fully visible
        }
        
        public bool ReadPixelBinary(int pixel)
        {
            uint sum = 0;
            // [0]...B, [1]...G, [2]..R, [3]..A
            sum += ColorArray[pixel * 4 + 0];  // blue
            sum += ColorArray[pixel * 4 + 1]; // green
            sum += ColorArray[pixel * 4 + 2];  // red
            if (sum < 128 * 3)
            {
                return false;
            }
            else
            {
                return true;
            }

        }

        public void addColorPacket(byte[] colorTexture2D, uint frameNr)
        {
            Buffer.BlockCopy(colorTexture2D, 0, ColorArray, 0, ColorArray.Length);
            
            // [0]...B, [1]...G, [2]..R, [3]..A
            SetPixelBinary(0, Convert.ToBoolean((frameNr & 0b_0000_0001) >> 0));
            SetPixelBinary(1, Convert.ToBoolean((frameNr & 0b_0000_0010) >> 1));
            SetPixelBinary(2, Convert.ToBoolean((frameNr & 0b_0000_0100) >> 2));
            SetPixelBinary(3, Convert.ToBoolean((frameNr & 0b_0000_1000) >> 3));
            SetPixelBinary(4, Convert.ToBoolean((frameNr & 0b_0001_0000) >> 4));
            SetPixelBinary(5, Convert.ToBoolean((frameNr & 0b_0010_0000) >> 5));
            SetPixelBinary(6, Convert.ToBoolean((frameNr & 0b_0100_0000) >> 6));
            SetPixelBinary(7, Convert.ToBoolean((frameNr & 0b_1000_0000) >> 7));
            
            SetPixelBinary(8, Convert.ToBoolean((frameNr & 0b_0000_0001_0000_0000) >> 8));
            SetPixelBinary(9, Convert.ToBoolean((frameNr & 0b_0000_0010_0000_0000) >> 9));
            SetPixelBinary(10, Convert.ToBoolean((frameNr & 0b_0000_0100_0000_0000) >> 10));
            SetPixelBinary(11, Convert.ToBoolean((frameNr & 0b_0000_1000_0000_0000) >> 11));
            SetPixelBinary(12, Convert.ToBoolean((frameNr & 0b_0001_0000_0000_0000) >> 12));
            SetPixelBinary(13, Convert.ToBoolean((frameNr & 0b_0010_0000_0000_0000) >> 13));
            SetPixelBinary(14, Convert.ToBoolean((frameNr & 0b_0100_0000_0000_0000) >> 14));
            SetPixelBinary(15, Convert.ToBoolean((frameNr & 0b_1000_0000_0000_0000) >> 15));

            SetPixelBinary(16, Convert.ToBoolean((frameNr & 0b_0000_0001_0000_0000_0000_0000) >> 16));
            SetPixelBinary(17, Convert.ToBoolean((frameNr & 0b_0000_0010_0000_0000_0000_0000) >> 17));
            SetPixelBinary(18, Convert.ToBoolean((frameNr & 0b_0000_0100_0000_0000_0000_0000) >> 18));
            SetPixelBinary(19, Convert.ToBoolean((frameNr & 0b_0000_1000_0000_0000_0000_0000) >> 19));
            SetPixelBinary(20, Convert.ToBoolean((frameNr & 0b_0001_0000_0000_0000_0000_0000) >> 20));
            SetPixelBinary(21, Convert.ToBoolean((frameNr & 0b_0010_0000_0000_0000_0000_0000) >> 21));
            SetPixelBinary(22, Convert.ToBoolean((frameNr & 0b_0100_0000_0000_0000_0000_0000) >> 22));
            SetPixelBinary(23, Convert.ToBoolean((frameNr & 0b_1000_0000_0000_0000_0000_0000) >> 23));
            
            // Control Pattern
            SetPixelBinary(24, true);
            SetPixelBinary(25, false);
            SetPixelBinary(26, true);
            SetPixelBinary(27, false);

            ColorPackets.Enqueue(ColorArray);
        }
    }
}