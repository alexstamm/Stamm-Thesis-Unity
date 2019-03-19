using Byn.Common;
using Byn.Media;
using Byn.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class VideoApp : MonoBehaviour
{
    //Call class handling all the functionality
    protected ICall mCall;

    //The UI is in a separate MonoBehaviour
    protected VideoAppUi mUi;

    //Contains the configuration used for the next call
    protected MediaConfig mMediaConfig;

    #region ServerConfigs
    [Header("Server Configs")]
    //This is a test server. Don't use in production! The server code is in a zip file in WebRtcNetwork
    public string uSignalingUrl = "ws://signaling.because-why-not.com/callapp";

    //By default the secure version is currently only used in WebGL builds as some browsers require.
    //Unity old mono version comes with a SSL implementation that can be quite slow and hangs sometimes
    public string uSecureSignalingUrl = "wss://signaling.because-why-not.com/callapp";

    //If set to true only the secure signaling url will be used.
    public bool uForceSecureSignaling = false;

    //Ice server is either a stun or a turn server used to get trough the firewall.
    //Warning: make sure the url is in a valid format and starts with stun: or turn:
    //WebRTC will try many different ways to connect the peers so if this server is not
    //available it might still be able to establish a direct connection or use the second ice server.
    //If you need more than two servers change the CreateNetworkConfig method.

    public string uIceServer = "stun:stun.because-why-not.com:443";
    public string uIceServerUser = "";
    public string uIceServerPassword = "";

    //Second ice server. As I can't guarantee the test server is always online.
    //If you need more than two servers or username / password then change the CreateNetworkConfig method.
    public string uIceServer2 = "stun:stun.l.google.com:19302";
    
    //Do not change. This length is enforced on the server side to avoid abuse.
    public const int MAX_CODE_LENGTH = 256;

    #endregion

    #region ServerVariables
    //Set to true after Join is called.
    //Set to false after either Join failed or the call ended / network failed / user exit
    private bool mCallActive = false;
    private string mUseAddress = null;
    protected MediaConfig mMediaConfigInUse;
    private ConnectionId mRemoteUserId = ConnectionId.INVALID;

    private bool mAutoRejoin = false;
    private float mRejoinTime = 2;

    private bool mLocalFrameEvents = true;

    //Used to backup the original sleep timeout value.
    //Will be restored after a call has ended
    private int mSleepTimeoutBackup;

    //For customization. Set to false to allow devices to sleep even if a call is active. 
    private bool mBlockSleep = true;

    #endregion

    #region Calls from unity

    protected virtual void Awake()
    {
        mUi = GetComponent<VideoAppUi>();
        Init();
        mMediaConfig = CreateMediaConfig();
        mMediaConfigInUse = mMediaConfig;
    }

    protected virtual void Start()
    {
        mUi.SetGuiState(true);

        //fill the video dropbox
        mUi.UpdateVideoDropdown();
    }

    private void OnDestroy()
    {
        CleanupCall();
    }

    //The call object needs to be updated regularly to sync data received via webrtc with unity.
    //All events will be triggered during the update method in the unity main thread to avoid multi threading errors
    protected virtual void Update()
    {
        if (mCall != null)
        {
            //update the call object. This will trigger all buffered events to be fired
            //to ensure it is done in the unity thread at a convenient time.
            mCall.Update();
        }
    }
    #endregion

    #region InitializeNetwork

    protected virtual void Init()
    {
        if (UnityCallFactory.Instance == null)
        {
            throw new InvalidOperationException("UnityCallFactory unavailable. Init process failed.");
        }
        //use for verbose log:
        //UnityCallFactory.Instance.SetDefaultLogger(true, true);
    }

    protected virtual NetworkConfig CreateNetworkConfig()
    {
        NetworkConfig netConfig = new NetworkConfig();
        if (string.IsNullOrEmpty(uIceServer) == false)
            netConfig.IceServers.Add(new IceServer(uIceServer, uIceServerUser, uIceServerPassword));
        if (string.IsNullOrEmpty(uIceServer2) == false)
            netConfig.IceServers.Add(new IceServer(uIceServer2));

        if (Application.platform == RuntimePlatform.WebGLPlayer || uForceSecureSignaling)
        {
            netConfig.SignalingUrl = uSecureSignalingUrl;
        }
        else
        {
            netConfig.SignalingUrl = uSignalingUrl;
        }

        if (string.IsNullOrEmpty(netConfig.SignalingUrl))
        {
            throw new InvalidOperationException("set signaling url is null or empty");
        }
        return netConfig;
    }

    //Creates the call object and uses the configure method to activate the video / audio support if the values are set to true.
    //Generating new frames after this call so the user can see himself before the call is connected.
    public virtual void SetupCall()
    {
        Append("Setting up ...");

        //hacks to turn off certain connection types. If both set to true only turn servers are used.
        //This helps simulating a NAT that doesn't support opening ports.
        
        //hack to turn off direct connections
        //Byn.Net.Native.AWebRtcPeer.sDebugIgnoreTypHost = true;
        //hack to turn off connections via stun servers
        //Byn.Net.Native.WebRtcDataPeer.sDebugIgnoreTypSrflx = true;

        NetworkConfig netConfig = CreateNetworkConfig();

        Debug.Log("Creating call using NetworkConfig:" + netConfig);
        //setup the server
        mCall = UnityCallFactory.Instance.Create(netConfig);
        if (mCall == null)
        {
            Append("Failed to create the call");
            return;
        }
        mCall.LocalFrameEvents = mLocalFrameEvents;
        string[] devices = UnityCallFactory.Instance.GetVideoDevices();
        if (devices == null || devices.Length == 0)
        {
            Debug.Log("no device found or no device information available");
        }
        else
        {
            foreach (string s in devices)
                Debug.Log("device found: " + s);
        }
        Append("Call created!");
        mCall.CallEvent += Call_CallEvent;

        //this happens in awake now to allow an ui or other external app to change media config before calling SetupCall
        //mMediaConfig = CreateMediaConfig();

        //make a deep clone to avoid confusion if settings are changed at runtime. 
        mMediaConfigInUse = mMediaConfig.DeepClone();
        Debug.Log("Configure call using MediaConfig: " + mMediaConfigInUse);
        mCall.Configure(mMediaConfigInUse);
        mUi.SetGuiState(false);

        if(mBlockSleep)
        {
            //backup sleep timeout and set it to never sleep
            mSleepTimeoutBackup = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }

    //Create the default configuration for this CallApp instance.
    //This can be overwritten in a subclass allowing the creation custom apps that use a slightly different configuration.
    public virtual MediaConfig CreateMediaConfig()
    {
        MediaConfig mediaConfig = new MediaConfig();
        //testing echo cancellation (native only)
        bool useEchoCancellation = false;
        if (useEchoCancellation)
        {
#if !UNITY_WEBGL
            var nativeConfig = new Byn.Media.Native.NativeMediaConfig();
            nativeConfig.AudioOptions.echo_cancellation = true;
            nativeConfig.AudioOptions.extended_filter_aec = true;
            nativeConfig.AudioOptions.delay_agnostic_aec = true;

            mediaConfig = nativeConfig;
#endif 
        }

        //use video and audio by default (the UI is toggled on by default as well it will change on click )
        mediaConfig.Audio = true;
        mediaConfig.Video = true;
        mediaConfig.VideoDeviceName = null;

        //This format is the only reliable format that works on all
        //platforms currently.
        mediaConfig.Format = FramePixelFormat.ABGR;

        mediaConfig.MinWidth = 160;
        mediaConfig.MinHeight = 120;
        //Larger resolutions are possible in theory but allowing users to set this too high is risky.
        //A lot of devices do have great cameras but not so great CPU's which might be unable to encode fast enough.
        mediaConfig.MaxWidth = 1920;
        mediaConfig.MaxHeight = 1080;

        //will be overwritten by UI in normal use
        mediaConfig.IdealWidth = 160;
        mediaConfig.IdealHeight = 120;
        mediaConfig.IdealFrameRate = 30;
        return mediaConfig;
    }
    #endregion

    #region HandleAppCallEvents

    //Handler of call events.
    //Can be customized in via subclasses.
    protected virtual void Call_CallEvent(object sender, CallEventArgs e)
    {
        switch (e.Type)
        {
            case CallEventType.CallAccepted:
                //Outgoing call was successful or an incoming call arrived
                Append("Connection established");
                mRemoteUserId = ((CallAcceptedEventArgs)e).ConnectionId;
                Debug.Log("New connection with id: " + mRemoteUserId
                    + " audio:" + mCall.HasAudioTrack(mRemoteUserId)
                    + " video:" + mCall.HasVideoTrack(mRemoteUserId));
                break;
            case CallEventType.CallEnded:
                //Call was ended / one of the users hung up -> reset the app
                Append("Call ended");
                InternalResetCall();
                break;
            case CallEventType.ListeningFailed:
                //listening for incoming connections failed
                //this usually means a user is using the string / room name already to wait for incoming calls
                //try to connect to this user
                //(note might also mean the server is down or the name is invalid in which case call will fail as well)
                mCall.Call(mUseAddress);
                break;
            case CallEventType.ConnectionFailed:
                {
                    Byn.Media.ErrorEventArgs args = e as Byn.Media.ErrorEventArgs;
                    Append("Connection failed error: " + args.ErrorMessage);
                    InternalResetCall();
                }
                break;
            case CallEventType.ConfigurationFailed:
                {
                    Byn.Media.ErrorEventArgs args = e as Byn.Media.ErrorEventArgs;
                    Append("Configuration failed error: " + args.ErrorMessage);
                    InternalResetCall();
                }
                break;
            case CallEventType.FrameUpdate:
                {

                    //new frame received from webrtc (either from local camera or network)
                    if (e is FrameUpdateEventArgs)
                    {
                        UpdateFrame((FrameUpdateEventArgs)e);
                    }
                    break;
                }
            case CallEventType.Message:
                {
                    //text message received
                    MessageEventArgs args = e as MessageEventArgs;
                    Append(args.Content);
                    break;
                }
            case CallEventType.WaitForIncomingCall:
                {
                    //the chat app will wait for another app to connect via the same string
                    WaitForIncomingCallEventArgs args = e as WaitForIncomingCallEventArgs;
                    Append("Waiting for incoming call address: " + args.Address);
                    break;
                }
        }
    }

    //Destroys the call. Used if unity destroys the object or if a call ended / failed due to an error.
    protected virtual void CleanupCall()
    {
        if (mCall != null)
        {
            mCallActive = false;
            mRemoteUserId = ConnectionId.INVALID;
            Debug.Log("Destroying call!");
            mCall.CallEvent -= Call_CallEvent;
            mCall.Dispose();
            mCall = null;
            //call the garbage collector. This isn't needed but helps discovering memory bugs early on.
            Debug.Log("Triggering garbage collection");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Debug.Log("Call destroyed");

            if (mBlockSleep)
            {
                //revert to the original value
                Screen.sleepTimeout = mSleepTimeoutBackup;
            }
        }
    }

    //Destroys the call object and shows the setup screen again.
    //Called after a call ends or an error occurred.
    public virtual void ResetCall()
    {
        //outside quits. don't rejoin automatically
        mAutoRejoin = false;
        InternalResetCall();
    }

    private void InternalResetCall()
    {
        CleanupCall();
        mUi.SetGuiState(true);
        if (mAutoRejoin)
            StartCoroutine(CoroutineRejoin());
    }

    //Allows to control the replay volume of the remote connection.
    //volume = between 0 and 1
    public virtual void SetRemoteVolume(float volume)
    {
        if (mCall == null)
            return;
        if(mRemoteUserId == ConnectionId.INVALID)
        {
            return;
        }
        mCall.SetVolume(volume, mRemoteUserId);
    }

    //Returns a list of video devices for the UI to show.
    //This is used to avoid having the UI directly access the UnityCallFactory.
    public string[] GetVideoDevices()
    {
        if (CanSelectVideoDevice())
        {
            List<string> devices = new List<string>();
            string[] videoDevices = UnityCallFactory.Instance.GetVideoDevices();
            devices.Add("Any");
            devices.AddRange(videoDevices);
            return devices.ToArray();
        }
        else
        {
            return new string[] { "Default" };
        };
    }

    #endregion

    #region HandleUICalls

    //Used by the UI
    public bool CanSelectVideoDevice()
    {
        return UnityCallFactory.Instance.CanSelectVideoDevice();
    }

    //Called by UI when the join buttin is pressed.
    public virtual void Join(string address)
    {
        if (address.Length > MAX_CODE_LENGTH)
            throw new ArgumentException("Address can't be longer than " + MAX_CODE_LENGTH);
        mUseAddress = address;
        InternalJoin();
    }

    private void InternalJoin()
    {
        if (mCallActive)
        {
            Debug.LogError("Join call failed. Call is already/still active");
            return;
        }
        Debug.Log("Try listing on address: " + mUseAddress);
        mCallActive = true;
        this.mCall.Listen(mUseAddress);
    }

    private IEnumerator CoroutineRejoin()
    {
        yield return new WaitForSecondsRealtime(mRejoinTime);
        SetupCall();
        InternalJoin();
    }

    //Called by ui to send a message.
    public virtual void Send(string msg)
    {
        this.mCall.Send(msg);
    }

    #endregion

    #region DecorateMediaConfigs

    //Turns on sending audio for the next call.
    public void SetAudio(bool value)
    {
        mMediaConfig.Audio = value;
    }

    //Turns on sending video for the next call.
    public void SetVideo(bool value)
    {
        mMediaConfig.Video = value;
    }

    //Allows to set a specific video device. This isn't supported on WebGL yet.
    public void SetVideoDevice(string deviceName)
    {
        mMediaConfig.VideoDeviceName = deviceName;
    }

    //Changes the target resolution that will be used for sending video streams.
    //The closest one the camera can handle will be used.
    public void SetIdealResolution(int width, int height)
    {
        mMediaConfig.IdealWidth = width;
        mMediaConfig.IdealHeight = height;
    }

    //Sets the ideal FPS.
    //This has a lower priority than the ideal resolution.
    //Note that the FPS aren't enforced. It pick the closest FPS the video device supports.
    public void SetIdealFps(int fps)
    {
        mMediaConfig.IdealFrameRate = fps;
    }

    //True will show the local video.
    //False will not return the video and thus save some CPU work.
    public void SetShowLocalVideo(bool showLocalVideo)
    {
        mLocalFrameEvents = showLocalVideo;
    }
    
    //Can be used to make the app automatically reconnect if a sudden disconnect occurred or the other side ends the connection.
    public void SetAutoRejoin(bool rejoin, float rejoinTime = 2)
    {
        mAutoRejoin = rejoin;
        mRejoinTime = rejoinTime;
    }

    //Forwarded to the call factory. Returns the loudspeaker status on mobile devices.
    public bool GetLoudspeakerStatus()
    {
        if(mCall != null)
        {
            return UnityCallFactory.Instance.GetLoudspeakerStatus();
        }
        return false;
    }

    //Sets the loudspeaker mode via the call factory.
    public void SetLoudspeakerStatus(bool state)
    {
        if (mCall != null)
        {
            UnityCallFactory.Instance.SetLoudspeakerStatus(state);
        }
    }

    //Set to true to mute the microphone.
    public void SetMute(bool state)
    {
        if (mCall != null)
        {
            mCall.SetMute(state);
        }
    }

    //True if the microphone is muted (or sending audio isn't active).
    public bool IsMute()
    {
        if (mCall != null)
        {
            return mCall.IsMute();
        }
        return true;
    }
    #endregion

    protected virtual void UpdateFrame(FrameUpdateEventArgs frameUpdateEventArgs)
    {
        //the avoid wasting CPU time the library uses the format returned by the browser -> ABGR little endian thus
        //the bytes are in order R G B A
        //Unity seem to use this byte order but also flips the image horizontally (reading the last row first?)
        //this is reversed using UI to avoid wasting CPU time

        //Debug.Log("frame update remote: " + frameUpdateEventArgs.IsRemote);

        if (frameUpdateEventArgs.IsRemote == false)
        {
            mUi.UpdateLocalTexture(frameUpdateEventArgs.Frame, frameUpdateEventArgs.Format);
        }
        else
        {
            mUi.UpdateRemoteTexture(frameUpdateEventArgs.Frame, frameUpdateEventArgs.Format);
        }
    }

    private void Append(string txt)
    {
        mUi.Append(txt);
    }
}
