using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public class IncomingScreenTransformHandler : MonoBehaviour
    {
        public bool localScreenPosition;
        private float lastTimeRead;
        public List<Transform> screens;
        
        private UnitySerializer deserializer;
        private void FixedUpdate()
        {
            byte[] incomingScreenTransforms = WsClient.incomingScreenTransforms;
            if (incomingScreenTransforms != null && localScreenPosition)
            {
                if (Time.time > (lastTimeRead + (1.0 * 2))) // FPS
                {
                    lastTimeRead = Time.time;

                    deserializer = new UnitySerializer(incomingScreenTransforms);
                    foreach (var screen in screens)
                    {
                        screen.position = IncomingFrameHandler.hologramTransform.position  +
                                          IncomingFrameHandler.hologramTransform.rotation * deserializer.DeserializeVector3();
                        screen.rotation = IncomingFrameHandler.hologramTransform.rotation * deserializer.DeserializeQuaternion();
                        screen.localScale = deserializer.DeserializeVector3();
                    }

                }
            }
        }

        public void toggleLocalScreenPosition()
        {
            localScreenPosition = !localScreenPosition;
        }
        
    }
    

}