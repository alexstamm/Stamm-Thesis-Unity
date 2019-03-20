using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Byn.Common;
using Byn.Media;
using Byn.Net;

public class ControllerStammThesis : MonoBehaviour {

    // Class Handling
    protected ICall _callRef;

    // Separate UI Behavior
    protected ViewStammThesis _uiRef;

    // Media Settings
    protected MediaConfig _mediaConfig;

    #region ServerConfigs
    [Header("Server Configs")]
    public string _signalingUrl = "ws://signaling.because-why-not.com/callapp";
    public string _secureSignalingUrl = "wss://signaling.because-why-not.com/callapp";

    //If set to true only the secure signaling url will be used.
    public bool _forceSecureSignaling = false;

    public string _iceServer = "stun:stun.because-why-not.com:443";
    public string _iceServerUser = "";
    public string _iceServerPassword = "";
    public string _iceServer2 = "stun:stun.l.google.com:19302";

    public const int MAX_CODE_LENGTH = 256;
    #endregion

    #region ServerVars
    private bool _callActive = false;
    private string _useAddress = null;
    protected MediaConfig _mediaConfigInUse;
    private ConnectionId _connectionId = ConnectionId.INVALID;

    //see if we need these
    private bool _autoRejoin = false;
    private float _rejoinTime = 2;

    private bool _localFrameEvents = true;
    private int _sleepTimeout;
    private bool _blockSleep = true;
    #endregion

    protected virtual void UpdateFrame(FrameUpdateEventArgs frameUpdateEventArgs)
    {
        if (frameUpdateEventArgs.IsRemote)
        {
            _uiRef.UpdateDisplayTexture(frameUpdateEventArgs.Frame, frameUpdateEventArgs.Format);
        }
    }

    #region UnityMonobehavior

    protected virtual void Awake()
    {
        _uiRef = GetComponent<ViewStammThesis>();
        InitErrorCheck();
        _mediaConfig = CreateMediaConfig();
        _mediaConfigInUse = _mediaConfig;
        
    }

    protected virtual void Start()
    {
        _uiRef.ShowDataPanel(true);
    }

    private void OnDestroy()
    {
        CleanupCall();
    }

    protected virtual void Update()
    {
        if (_callRef != null)
        {
            _callRef.Update();
            
        }
    }

    #endregion

    #region CustomCalls

    public virtual void SendPos(string s)
    {
        this._callRef.Send(s);
    }

    public virtual void SendPosData(byte[] bytes)
    {
        this._callRef.Send(bytes, true);
    }

    public virtual void SendTransformData(byte[] bytes)
    {
        this._callRef.Send(bytes, true);
    }

    #endregion


    #region InitNetwork

    protected virtual void InitErrorCheck()
    {
        if(UnityCallFactory.Instance == null)
        {
            throw new InvalidOperationException("UnityCallFactory unavailable");
        }
    }
    protected virtual NetworkConfig CreateNetworkConfig()
    {
        NetworkConfig NetConfig = new NetworkConfig();
        if (string.IsNullOrEmpty(_iceServer) == false)
            NetConfig.IceServers.Add(new IceServer(_iceServer, _iceServerUser, _iceServerPassword));
        if (string.IsNullOrEmpty(_iceServer2) == false)
            NetConfig.IceServers.Add(new IceServer(_iceServer2));

        NetConfig.SignalingUrl = _signalingUrl;

        if (string.IsNullOrEmpty(NetConfig.SignalingUrl))
        {
            throw new InvalidOperationException("set signaling url is null or empty");
        }

        return NetConfig;
    }

    public virtual MediaConfig CreateMediaConfig()
    {
        MediaConfig MedConfig = new MediaConfig();
        
        // No Audio
        MedConfig.Audio = false;
        MedConfig.Video = true;
        MedConfig.VideoDeviceName = null;

        MedConfig.Format = FramePixelFormat.ABGR;

        MedConfig.MinWidth = 1024;
        MedConfig.MinHeight = 1024;

        MedConfig.MaxWidth = 1024;
        MedConfig.MaxHeight = 1024;

        MedConfig.IdealWidth = 1024;
        MedConfig.IdealHeight = 1024;
        MedConfig.IdealFrameRate = 60;

        return MedConfig;
    }

    public virtual void SetupConnection()
    {
        NetworkConfig NetConfig = CreateNetworkConfig();

        Debug.Log("Creating NetworkConfig:" + NetConfig);

        _callRef = UnityCallFactory.Instance.Create(NetConfig);
        if(_callRef == null)
        {
            Debug.Log("Failed to connect");
            return;
        }

        //don't display local frames to screen, only render to buffer then package for sending
        _callRef.LocalFrameEvents = false;

        string[] AllVideoDevices = UnityCallFactory.Instance.GetVideoDevices();
        if(AllVideoDevices == null || AllVideoDevices.Length == 0)
        {
            Debug.Log("Cannot find virtual video device");
        }
        else
        {
            foreach(string value in AllVideoDevices)
            {
                Debug.Log("camera found: " + value);
            }
        }
        Debug.Log("Connection Succeeded");
        _callRef.CallEvent += ConnectionHandler;

        _mediaConfigInUse = _mediaConfig.DeepClone();
        Debug.Log("Media Config: " + _mediaConfigInUse);
        _callRef.Configure(_mediaConfigInUse);

        if (_blockSleep)
        {
            _sleepTimeout = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }

    #endregion

    #region HandleCallEvents

    protected virtual void ConnectionHandler(object sender, CallEventArgs e)
    {
        switch(e.Type){
            case CallEventType.CallAccepted:
                _connectionId = ((CallAcceptedEventArgs)e).ConnectionId;
                Debug.Log("New server id: " + _connectionId);
                break;
            case CallEventType.CallEnded:
                Debug.Log("Call ended");
                InternalResetConnection();
                break;
            case CallEventType.ListeningFailed:
                _callRef.Call(_useAddress);
                break;
            case CallEventType.ConnectionFailed:
                {
                    Byn.Media.ErrorEventArgs args = e as Byn.Media.ErrorEventArgs;
                    Debug.Log("Connection failed" + args.ErrorMessage);
                    InternalResetConnection();
                }
                break;
            case CallEventType.ConfigurationFailed:
                {
                    Byn.Media.ErrorEventArgs args = e as Byn.Media.ErrorEventArgs;
                    Debug.Log("Configuration failed: " + args.ErrorMessage);
                    InternalResetConnection();
                }
                break;
            case CallEventType.FrameUpdate:
                {
                    if (e is FrameUpdateEventArgs)
                    {
                        UpdateFrame((FrameUpdateEventArgs)e);
                    }
                    break;
                }
            case CallEventType.WaitForIncomingCall:
                {
                    WaitForIncomingCallEventArgs args = e as WaitForIncomingCallEventArgs;
                    Debug.Log("Waiting for incoming call address: " + args.Address);
                    break;
                }
            case CallEventType.Message:
                {
                    MessageEventArgs args = e as MessageEventArgs;
                    _uiRef.DisplayReceivedPos(args.Content);
                    break;
                }
            case CallEventType.DataMessage:
                {
                    DataMessageEventArgs args = e as DataMessageEventArgs;
                    _uiRef.HandlePosDataMessage(args.Content);
                    
                    break;
                }
                
        }
    }

    protected virtual void CleanupCall()
    {
        if(_callRef != null)
        {
            _callActive = false;
            _connectionId = ConnectionId.INVALID;
            Debug.Log("Destroying Connection");
            _callRef.CallEvent -= ConnectionHandler;
            _callRef.Dispose();
            _callRef = null;

            Debug.Log("picking up garbage...");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Debug.Log("Connectioned Destroyed");

            if (_blockSleep)
            {
                Screen.sleepTimeout = _sleepTimeout;
            }
        }
    }

    public virtual void ResetConnection()
    {
        _autoRejoin = false;
        InternalResetConnection();
    }

    private void InternalResetConnection()
    {
        CleanupCall();
        _uiRef.ShowDataPanel(true);
        if (_autoRejoin)
            StartCoroutine(CoroutineRejoin());
    }

    #endregion

    #region HandleUICalls

    public virtual void Join(string address)
    {
        if (address.Length > MAX_CODE_LENGTH)
            throw new ArgumentException("Address can't be longer than " + MAX_CODE_LENGTH);
        _useAddress = address;
        InternalJoin();
    }

    private void InternalJoin()
    {
        if (_callActive)
        {
            Debug.Log("Connection already active");
            return;
        }
        Debug.Log("Listening: " + _useAddress);
        _callActive = true;
        this._callRef.Listen(_useAddress);
    }

    private IEnumerator CoroutineRejoin()
    {
        yield return new WaitForSecondsRealtime(_rejoinTime);
        SetupConnection();
        InternalJoin();
    }

    #endregion

    #region SetMediaConfigs

    public void SetVideo(bool value)
    {
        _mediaConfig.Video = value;
    }

    public void SetIdealResolution(int width, int height)
    {
        _mediaConfig.IdealWidth = width;
        _mediaConfig.IdealHeight = height;
    }

    public void SetIdealFps(int fps)
    {
        _mediaConfig.IdealFrameRate = fps;
    }

    // auto reconnect
    public void SetAutioRejoin(bool rejoin, float rejoinTime = 2)
    {
        _autoRejoin = rejoin;
        _rejoinTime = rejoinTime;
    }

    #endregion

}
