using Telepresence;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public static class HandData
    {
        public static byte[] HandArray {get; set; }
        
        public static void initHand()
        {
            HandArray = new byte[CommonParameters.handDataSize];
        }
    }
}