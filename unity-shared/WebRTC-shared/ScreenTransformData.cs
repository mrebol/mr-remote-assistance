using Telepresence;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public static class ScreenTransformData
    {
        public static byte[] ScreenTransformArray {get; set; }
        
        public static void initialize()
        {
            ScreenTransformArray = new byte[CommonParameters.screenTransformDataSize];
        }
    }
}