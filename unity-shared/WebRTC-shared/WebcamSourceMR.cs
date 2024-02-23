// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using global::Windows.Graphics.Holographic;
#endif

#if UNITY_WSA && !UNITY_EDITOR
using System.Threading.Tasks;
using global::Windows.UI.Core;
using global::Windows.Foundation;
using global::Windows.Media.Core;
using global::Windows.Media.Capture;
using global::Windows.ApplicationModel.Core;
#endif

#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Microsoft.MixedReality.WebRTC.Unity
{

    /// <summary>
    /// This component represents a local video sender generating video frames from a local
    /// video capture device (webcam).
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Webcam Source MR")]
    public class WebcamSourceMR : VideoTrackSource
    {
        public byte selectWebcam= 0;
        
        /// <summary>
        /// Optional identifier of the webcam to use. Setting this value forces using the given
        /// webcam, and will fail opening any other webcam.
        /// Valid values are obtained by calling <see cref="PeerConnection.GetVideoCaptureDevicesAsync"/>.
        /// </summary>
        /// <remarks>
        /// This property is purposely not shown in the Unity inspector window, as there is very
        /// little reason to hard-code a value for it, which would only work on a specific device
        /// with a given immutable hardware. It is still serialized on the off-chance that there
        /// is a valid use case for hard-coding it.
        /// </remarks>
        /// <seealso cref="PeerConnection.GetVideoCaptureDevicesAsync"/>
        [HideInInspector]
        public VideoCaptureDevice WebcamDevice = default;

        /// <summary>
        /// Enable Mixed Reality Capture (MRC) if available on the local device.
        /// This option has no effect on devices not supporting MRC, and is silently ignored.
        /// </summary>
        [Tooltip("Enable Mixed Reality Capture (MRC) if available on the local device")]
        public bool EnableMixedRealityCapture = true;

        /// <summary>
        /// Enable the on-screen recording indicator when Mixed Reality Capture (MRC) is
        /// available and enabled.
        /// This option has no effect on devices not supporting MRC, or if MRC is not enabled.
        /// </summary>
        [Tooltip("Enable the on-screen recording indicator when MRC is enabled")]
        public bool EnableMRCRecordingIndicator = true;

        /// <summary>
        /// Selection mode for the video capture format.
        /// </summary>
        public LocalVideoSourceFormatMode FormatMode = LocalVideoSourceFormatMode.Automatic;

        /// <summary>
        /// For manual <see cref="FormatMode"/>, unique identifier of the video profile to use,
        /// or an empty string to leave unconstrained.
        /// </summary>
        public string VideoProfileId = string.Empty;

        /// <summary>
        /// For manual <see cref="FormatMode"/>, kind of video profile to use among a list of predefined
        /// ones, or an empty string to leave unconstrained.
        /// </summary>
        public VideoProfileKind VideoProfileKind = VideoProfileKind.Unspecified;

        /// <summary>
        /// For manual <see cref="FormatMode"/>, optional constraints on the resolution and framerate of
        /// the capture format. These constraints are additive, meaning a matching format must satisfy
        /// all of them at once, in addition of being restricted to the formats supported by the selected
        /// video profile or kind of profile. Any negative or zero value means no constraint.
        /// </summary>
        /// <remarks>
        /// Video capture formats for HoloLens 1 and HoloLens 2 are available here:
        /// https://docs.microsoft.com/en-us/windows/mixed-reality/locatable-camera
        /// </remarks>
        public VideoCaptureConstraints Constraints = new VideoCaptureConstraints()
        {
            width = 0,
            height = 0,
            framerate = 0.0
        };

#if !UNITY_EDITOR && UNITY_ANDROID
        protected bool _androidCameraRequestPending = false;
        protected float _androidCameraRequestRetryUntilTime = 0f;
#endif

        protected async void OnEnable()
        {
            if (Source != null)
            {
                return;
            }

#if !UNITY_EDITOR && UNITY_ANDROID
            // Ensure Android binding is initialized before accessing the native implementation
            Android.Initialize();

            // Check for permission to access the camera
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                if (!_androidCameraRequestPending)
                {
                    // Monitor the OnApplicationFocus(true) event during the next 5 minutes,
                    // and check for permission again each time (see below why).
                    _androidCameraRequestPending = true;
                    _androidCameraRequestRetryUntilTime = Time.time + 300;

                    // Display dialog requesting user permission. This will return immediately,
                    // and unfortunately there's no good way to tell when this completes. As a rule
                    // of thumb, application should lose focus, so check when focus resumes should
                    // be sufficient without having to poll every frame.
                    Permission.RequestUserPermission(Permission.Camera);
                }
                return;
            }
#elif UNITY_WSA && !UNITY_EDITOR
            // Request UWP access to video capture. The OS may show some popup dialog to the
            // user to request permission. This will succeed only if the user grants permission.
            try
            {
                // Note that the UWP UI thread and the main Unity app thread are always different.
                // https://docs.unity3d.com/Manual/windowsstore-appcallbacks.html
                // We leave the code below as an example of generic handling in case this would be used in
                // some other place, and in case a future version of Unity decided to change that assumption,
                // but currently OnEnable() is always invoked from the main Unity app thread so here the first
                // branch is never taken.
                if (UnityEngine.WSA.Application.RunningOnUIThread())
                {
                    await RequestAccessAsync();
                }
                else
                {
                    UnityEngine.WSA.Application.InvokeOnUIThread(() => RequestAccessAsync(), waitUntilDone: true);
                }
            }
            catch (Exception ex)
            {
                // Log an error and prevent activation
                Debug.LogError($"Video access failure: {ex.Message}.");
                this.enabled = false;
                return;
            }
#endif

            // Handle automatic capture format constraints
            string videoProfileId = VideoProfileId;
            var videoProfileKind = VideoProfileKind;
            int width = Constraints.width;
            int height = Constraints.height;
            double framerate = Constraints.framerate;
#if ENABLE_WINMD_SUPPORT
            if (FormatMode == LocalVideoSourceFormatMode.Automatic)
            {
                // Do not constrain resolution by default, unless the device calls for it (see below).
                width = 0; // auto
                height = 0; // auto

                // Avoid constraining the framerate; this is generally not necessary (formats are listed
                // with higher framerates first) and is error-prone as some formats report 30.0 FPS while
                // others report 29.97 FPS.
                framerate = 0; // auto

                // For HoloLens, use video profile to reduce resolution and save power/CPU/bandwidth
                if (global::Windows.Graphics.Holographic.HolographicSpace.IsAvailable)
                {
                    if (!global::Windows.Graphics.Holographic.HolographicDisplay.GetDefault().IsOpaque)
                    {
                        if (global::Windows.ApplicationModel.Package.Current.Id.Architecture == global::Windows.System.ProcessorArchitecture.X86)
                        {
                            // Holographic AR (transparent) x86 platform - Assume HoloLens 1
                            videoProfileKind = WebRTC.VideoProfileKind.VideoRecording; // No profile in VideoConferencing
                            width = 896; // Target 896 x 504
                        }
                        else
                        {
                            // Holographic AR (transparent) non-x86 platform - Assume HoloLens 2
                            videoProfileKind = WebRTC.VideoProfileKind.VideoConferencing;
                            width = 960; // Target 960 x 540
                        }
                    }
                }
            }
#elif !UNITY_EDITOR && UNITY_ANDROID
            if (FormatMode == LocalVideoSourceFormatMode.Automatic)
            {
                // Avoid constraining the framerate; this is generally not necessary (formats are listed
                // with higher framerates first) and is error-prone as some formats report 30.0 FPS while
                // others report 29.97 FPS.
                framerate = 0; // auto

                string deviceId = WebcamDevice.id;
                if (string.IsNullOrEmpty(deviceId))
                {
                    IReadOnlyList<VideoCaptureDevice> listedDevices = await PeerConnection.GetVideoCaptureDevicesAsync();
                    if (listedDevices.Count > 0)
                    {
                        deviceId = listedDevices[0].id;
                    }
                }
                if (!string.IsNullOrEmpty(deviceId))
                {
                    // Find the closest format to 720x480, independent of framerate
                    IReadOnlyList<VideoCaptureFormat> formats = await DeviceVideoTrackSource.GetCaptureFormatsAsync(deviceId);
                    double smallestDiff = double.MaxValue;
                    bool hasFormat = false;
                    foreach (var fmt in formats)
                    {
                        double diff = Math.Abs(fmt.width - 720) + Math.Abs(fmt.height - 480);
                        if ((diff < smallestDiff) || !hasFormat)
                        {
                            hasFormat = true;
                            smallestDiff = diff;
                            width = (int)fmt.width;
                            height = (int)fmt.height;
                        }
                    }
                    if (hasFormat)
                    {
                        Debug.Log($"WebcamSource automated mode selected resolution {width}x{height} for Android video capture device #{deviceId}.");
                    }
                }
            }
#endif

            // TODO - Fix codec selection (was as below before change)

            // Force again PreferredVideoCodec right before starting the local capture,
            // so that modifications to the property done after OnPeerInitialized() are
            // accounted for.
            //< FIXME
            //PeerConnection.Peer.PreferredVideoCodec = PreferredVideoCodec;

            // Check H.264 requests on Desktop (not supported)
            //#if !ENABLE_WINMD_SUPPORT
            //            if (PreferredVideoCodec == "H264")
            //            {
            //                Debug.LogError("H.264 encoding is not supported on Desktop platforms. Using VP8 instead.");
            //                PreferredVideoCodec = "VP8";
            //            }
            //#endif

            var WebcamSources = DeviceVideoTrackSource.GetCaptureDevicesAsync().Result;
            foreach (var videoCaptureDevice in WebcamSources)
            {
                print(videoCaptureDevice.id + "||" + videoCaptureDevice.name);
            }

            
            print("current: " + WebcamDevice.id +"||" +  WebcamDevice.name);
            // Create the track
            var deviceConfig = new LocalVideoDeviceInitConfig
            {
                videoDevice = WebcamSources[selectWebcam],
                videoProfileId = videoProfileId,
                videoProfileKind = videoProfileKind,
                width = (width > 0 ? (uint?)width : null),
                height = (height > 0 ? (uint?)height : null),
                framerate = (framerate > 0 ? (double?)framerate : null),
                enableMrc = EnableMixedRealityCapture,
                enableMrcRecordingIndicator = EnableMRCRecordingIndicator
            };
            try
            {
                var source = await DeviceVideoTrackSource.CreateAsync(deviceConfig);
                AttachSource(source);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create device track source for {nameof(WebcamSource)} component '{name}'.");
                Debug.LogException(ex, this);
                return;
            }
        }

#if !UNITY_EDITOR && UNITY_ANDROID
        protected void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                return;
            }

            // If focus is restored after a pending camera access request, check the permission again
            if (_androidCameraRequestPending)
            {
                _androidCameraRequestPending = false;

                if (Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    // If now authorized, start capture as if just enabled
                    Debug.Log("User granted authorization to access webcam, starting WebcamSource now...");
                    OnEnable();
                }
                else if (Time.time <= _androidCameraRequestRetryUntilTime)
                {
                    // OnApplicationFocus(true) may be called for unrelated reason(s) so do not disable on first call,
                    // but instead retry during a given period after the request was made, until we're reasonably
                    // confident that the user dialog was actually answered (that is, that OnApplicationFocus(true) was
                    // called because of that dialog, and not because of another reason).
                    // This may lead to false positives (checking permission after the user denied it), but the user
                    // dialog will not popup again, so this is all in the background and essentially harmless.
                    _androidCameraRequestPending = true;
                }
                else
                {
                    // Some reasonable time passed since we made the permission request, and we still get a denied
                    // answer, so assume the user actually denied it and stop retrying.
                    _androidCameraRequestRetryUntilTime = 0f;
                    Debug.LogError("User denied Camera permission; cannot use WebcamSource. Forcing enabled=false.");
                    enabled = false;
                }
            }
        }
#endif

        protected void OnDisable()
        {
            DisposeSource();
        }

#if UNITY_WSA && !UNITY_EDITOR
        /// <summary>
        /// Internal UWP helper to ensure device access.
        /// </summary>
        /// <remarks>
        /// This must be called from the main UWP UI thread (not the main Unity app thread).
        /// </remarks>
        private Task RequestAccessAsync()
        {
            // On UWP the app must have the "webcam" capability, and the user must allow webcam
            // access. So check that access before trying to initialize the WebRTC library, as this
            // may result in a popup window being displayed the first time, which needs to be accepted
            // before the camera can be accessed by WebRTC.
            var mediaAccessRequester = new MediaCapture();
            var mediaSettings = new MediaCaptureInitializationSettings();
            mediaSettings.AudioDeviceId = "";
            mediaSettings.VideoDeviceId = "";
            mediaSettings.StreamingCaptureMode = StreamingCaptureMode.Video;
            mediaSettings.PhotoCaptureSource = PhotoCaptureSource.VideoPreview;
            mediaSettings.SharingMode = MediaCaptureSharingMode.SharedReadOnly; // for MRC and lower res camera
            return mediaAccessRequester.InitializeAsync(mediaSettings).AsTask();
        }
#endif
    }
}
