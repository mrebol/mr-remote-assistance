# mr-remote-assistance
Mixed Reality Real-Time Remote Assistance 

This project emerged from a collaboration between American University and The George Washington University. 

The work was supported by National Science Foundation grant no. 2026505 and 2026568.

## Folders
`MR-Local-Computer` Unity project that runs on local computer with Azure Kinect Cameras.

`MR-Local-Hololens` Unity project that streams (Holographic Remoting) the the local operator's Hololens.

`MR-Remote-Computer` Unity project that runs on remote computer with webcam (optional).

`MR-Remote-Hololens` Unity project that streams (Holographic Remoting) the the remote expert's Hololens.

`system-data` Calibration data and python callibration script.

`unity-shard` C# scripts that are shared between the Unity projects.

`ws-server` Websocket server for the communication between local and remote. 


## Dependencies
+ Unity 2020.3.42
+ NodeJS
+ Python

## Resources
+ [Mixed Reality WebRTC](https://github.com/microsoft/MixedReality-WebRTC)
+ Nuget package: [WebSocketSharp-netstandard](https://www.nuget.org/packages/WebSocketSharp-netstandard)
+ [Azure Kinect Sensor SDK](https://github.com/microsoft/Azure-Kinect-Sensor-SDK)

## Hardware
+ Microsoft Azure Kinect
+ Microsoft Hololens 2


## Preparation
Download and run the [node-dss WebRTC Signaling server](https://github.com/bengreenier/node-dss):

Inside the node-dss folder: `npm install`

Run the singalling server:

    set DEBUG=dss* && cd node-dss-master && npm start
    

Run the NodeJS websocket server:

Inside the ws-server folder: `node install`

    node ws-server/server.js


Run the commands inside `unity-shared/link-commands.txt` to create the system links. 

Download and run the [Mixed Reality Feature Tool](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/unity/welcome-to-mr-feature-tool ):

Install the following packages for the 2 Unity projects `MR-Local-Hololens` and `MR-Remote-Hololens`:
+ Mixed Reality Toolkit Examples 2.7.3
+ Mixed Reality Toolkit Extensions 2.7.3
+ Mixed Reality Toolkit Foundation 2.7.3
+ Mixed Reality Toolkit Standard Assets 2.7.3
+ Mixed Reality Toolkit Test Utilities 2.7.3
+ Mixed Reality Toolkit Tools 2.7.3
+ Mixed Reality OpenXR Plugin 1.2.1
