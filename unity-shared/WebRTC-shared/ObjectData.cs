using Telepresence;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public static class ObjectData
    {
        public static byte[] ObjectArray {get; set; }
        
        public static void initObject()
        {
            ObjectArray = new byte[CommonParameters.objectDataSize];
        }
    }
}