using System.Threading.Tasks;
using UnityEngine;

namespace Telepresence
{
    public class DepthSmoothing 
    {
      public FixedSizedQueue<ushort[]> depthQueue;


      public DepthSmoothing()
      {
        depthQueue = new FixedSizedQueue<ushort[]>(2);
        
      }
        public ushort[] CreateDepthArray(byte[] image)
        {
            ushort[] returnArray = new ushort[CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth];
            byte[] depthFrame = image;

            // Process each row in parallel
            Parallel.For(0, CommonParameters.depthSendingHeight, depthImageRowIndex =>
            {
                // Process each pixel in the row
                for (int depthImageColumnIndex = 0; depthImageColumnIndex < CommonParameters.depthSendingWidth*2; depthImageColumnIndex += 2)
                {
                    var depthIndex = depthImageColumnIndex + (depthImageRowIndex * CommonParameters.depthSendingWidth*2);
                    var index = depthIndex / 2;

                    returnArray[index] = 
                        CalculateDistanceFromDepth(depthFrame[depthIndex], depthFrame[depthIndex + 1]);
                }
            });

            return returnArray;
        }
        
        public byte[] CreateDepthByteArray(ushort[] image)
        {
          byte[] returnArray = new byte[CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth * 2];
          ushort[] depthFrame = image;

          // Process each row in parallel
          Parallel.For(0, CommonParameters.depthSendingHeight, depthImageRowIndex =>
          {
            // Process each pixel in the row
            for (int depthImageColumnIndex = 0; depthImageColumnIndex < CommonParameters.depthSendingWidth*2; depthImageColumnIndex += 2)
            {
              var depthIndex = depthImageColumnIndex + (depthImageRowIndex * CommonParameters.depthSendingWidth*2);
              var index = depthIndex / 2;

              returnArray[depthIndex] = (byte) (depthFrame[index] & 0xffff);
              returnArray[depthIndex+1] = (byte) (depthFrame[index] & 0xffff0000);
            }
          });

          return returnArray;
        }
        
        private ushort CalculateDistanceFromDepth(byte first, byte second)
        {
            // Please note that this would be different if you 
            // use Depth and User tracking rather than just depth
            return (ushort)(first | second << 8);
        }


        public ushort[] tempAvg()
        {


          int[] sumDepthArray = new int[CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth];
          ushort[] averagedDepthArray = new ushort[CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth];

          int Denominator = 0;
          int Count = 1;

// REMEMBER!!! Queue's are FIFO (first in, first out). 
// This means that when you iterate over them, you will
// encounter the oldest frame first.

// We first create a single array, summing all of the pixels 
// of each frame on a weighted basis and determining the denominator
// that we will be using later.
          foreach (var item in depthQueue)
          {
            // Process each row in parallel
            Parallel.For(0,CommonParameters.depthSendingHeight, depthArrayRowIndex =>
            {
              // Process each pixel in the row
              for (int depthArrayColumnIndex = 0; depthArrayColumnIndex < CommonParameters.depthSendingWidth; depthArrayColumnIndex++)
              {
                var index = depthArrayColumnIndex + (depthArrayRowIndex * CommonParameters.depthSendingWidth);
                sumDepthArray[index] += item[index] * Count;
              }
            });
            Denominator += Count;
            Count++;
          }

// Once we have summed all of the information on a weighted basis,
// we can divide each pixel by our denominator to get a weighted average.
          Parallel.For(0, CommonParameters.depthSendingHeight * CommonParameters.depthSendingWidth, i =>
          {
            averagedDepthArray[i] = (ushort)(sumDepthArray[i] / Denominator);
          });
          return averagedDepthArray;
        }
    }
}