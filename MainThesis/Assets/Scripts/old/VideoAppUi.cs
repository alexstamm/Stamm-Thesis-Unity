using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using Byn.Media;
using Byn.Media.Native;

public class VideoAppUi : MonoBehaviour
{
    #region MainRefs
    [Header("Main References")]
    //PlaybackGO
    public GameObject PlaybackSphere;

    //Texture of the local video
    protected Texture2D mLocalVideoTexture = null;

    //Texture of the remote video
    protected Texture2D mRemoteVideoTexture = null;

    //Display of the local camera
    public RawImage uLocalVideoImage;

    //Display of the remote camera
    public RawImage uRemoteVideoImage;

    //Debug Material
    public Material debugMat;

    private string mStoredVideoDevice = null;

    private IEnumerator autoJoinTimer;

    protected VideoApp mApp;

    #endregion

    #region PublicUIElements

    [Header("Setup panel")]
    //Panel with the join button. Will be hidden after setup
    public RectTransform uSetupPanel;
    public RectTransform uMainSetupPanel;

    //Input field used to enter the room name.
    public InputField uRoomNameInputField;
    //Join button to connect to a server.
    public Button uJoinButton;

    public Toggle uAudioToggle;
    public Toggle uVideoToggle;
    public Dropdown uVideoDropdown;

    [Header("Settings panel")]
    public RectTransform uSettingsPanel;
    public InputField uIdealWidth;
    public InputField uIdealHeight;
    public InputField uIdealFps;
    public Toggle uRejoinToggle;
    public Toggle uLocalVideoToggle;

    [Header("Video and Chat panel")]
    public RectTransform uInCallBase;
    public RectTransform uVideoPanel;
    public RectTransform uChatPanel;
    public RectTransform uVideoOverlay;

    [Header("Default positions/transformations")]
    public RectTransform uVideoBase;
    public RectTransform uChatBase;

    [Header("Fullscreen positions/transformations")]
    public RectTransform uFullscreenPanel;
    public RectTransform uVideoBaseFullscreen;
    public RectTransform uChatBaseFullscreen;

    #endregion

    #region ChatPanelElements
    [Header("Chat panel elements")]

    //Input field to enter a new message.
    public InputField uMessageInputField;

    //Output message list to show incoming and sent messages + output messages of the system itself.
    public MessageList uMessageOutput;

    //Send button.
    public Button uSendMessageButton;

    //Shutdown button. Disconnects all connections + shuts down the server if started.
    public Button uShutdownButton;

    //Toggle to switch the microphone on / off. 
    public Toggle uMuteToggle;

    //Toggle to switch the loudspeakers on / off. Only for mobile visible.
    public Toggle uLoudspeakerToggle;

    //Slider to just the remote users volume.
    public Slider uVolumeSlider;

    //Slider to just the remote users volume.
    public Text uOverlayInfo;

    #endregion

    #region CameraTextureSettings

    [Header("Camera Texture Settings")]
    public Texture2D uNoCameraTexture;

    protected bool mFullscreen = false;
    protected bool mDisplayRenderTexture = false;

    private float mFpsTimer = 0;
    private float mVideoOverlayTimeout = 0;
    private static readonly float sDefaultOverlayTimeout = 8;

    private bool mHasLocalVideo = false;
    private int mLocalVideoWidth = -1;
    private int mLocalVideoHeight = -1;
    private int mLocalFps = 0;
    private int mLocalFrameCounter = 0;
    private FramePixelFormat mLocalVideoFormat = FramePixelFormat.Invalid;

    private bool mHasRemoteVideo = false;
    private int mRemoteVideoWidth = -1;
    private int mRemoteVideoHeight = -1;
    private int mRemoteFps = 0;
    private int mRemoteFrameCounter = 0;
    private FramePixelFormat mRemoteVideoFormat = FramePixelFormat.Invalid;

    private string mPrefix = "CallAppUI_";
    private static readonly string PREF_AUDIO = "audio";
    private static readonly string PREF_VIDEO = "video";
    private static readonly string PREF_VIDEODEVICE = "videodevice";
    private static readonly string PREF_ROOMNAME = "roomname";
    private static readonly string PREF_IDEALWIDTH = "idealwidth";
    private static readonly string PREF_IDEALHEIGHT = "idealheight";
    private static readonly string PREF_IDEALFPS = "idealfps";
    private static readonly string PREF_REJOIN = "rejoin";
    private static readonly string PREF_LOCALVIDEO = "localvideo";

    #endregion

    #region InitFunctions
    protected virtual void Awake()
    {
        mApp = GetComponent<VideoApp>();

        if (Application.isMobilePlatform == false)
            uLoudspeakerToggle.gameObject.SetActive(false);
        mPrefix += this.gameObject.name + "_";
        LoadSettings();

        //add auto join timer
        autoJoinTimer = WaitToJoin(3.0f);
        StartCoroutine(autoJoinTimer);
    }

    protected virtual void Start()
    {
        if (this.uVideoOverlay != null)
        {
            this.uVideoOverlay.gameObject.SetActive(false);
        }
    }

    private IEnumerator WaitToJoin(float time)
    {
        yield return new WaitForSeconds(time);
        JoinButtonPressed();
    }

    //Decorate network settings
    private void SaveSettings()
    {
        PlayerPrefsSetBool(mPrefix + PREF_AUDIO, uAudioToggle.isOn);
        PlayerPrefsSetBool(mPrefix + PREF_VIDEO, uVideoToggle.isOn);
        PlayerPrefs.SetString(mPrefix + PREF_VIDEODEVICE, GetSelectedVideoDevice());
        PlayerPrefs.SetString(mPrefix + PREF_ROOMNAME, uRoomNameInputField.text);
        PlayerPrefs.SetString(mPrefix + PREF_IDEALWIDTH, uIdealWidth.text);
        PlayerPrefs.SetString(mPrefix + PREF_IDEALHEIGHT, uIdealHeight.text);
        PlayerPrefs.SetString(mPrefix + PREF_IDEALFPS, uIdealFps.text);
        PlayerPrefsSetBool(mPrefix + PREF_REJOIN, uRejoinToggle.isOn);
        PlayerPrefsSetBool(mPrefix + PREF_LOCALVIDEO, uLocalVideoToggle.isOn);
        PlayerPrefs.Save();
    }

    //Loads the ui state from last use
    private void LoadSettings()
    {
        uAudioToggle.isOn = PlayerPrefsGetBool(mPrefix + PREF_AUDIO, true);
        uVideoToggle.isOn = PlayerPrefsGetBool(mPrefix + PREF_VIDEO, true);
        //can't select this immediately because we don't know if it is valid yet
        mStoredVideoDevice = PlayerPrefs.GetString(mPrefix + PREF_VIDEODEVICE, null);
        uRoomNameInputField.text = PlayerPrefs.GetString(mPrefix + PREF_ROOMNAME, uRoomNameInputField.text);
        uIdealWidth.text = PlayerPrefs.GetString(mPrefix + PREF_IDEALWIDTH, "320");
        uIdealHeight.text = PlayerPrefs.GetString(mPrefix + PREF_IDEALHEIGHT, "240");
        uIdealFps.text = PlayerPrefs.GetString(mPrefix + PREF_IDEALFPS, "30");
        uRejoinToggle.isOn = PlayerPrefsGetBool(mPrefix + PREF_REJOIN, false);
        uLocalVideoToggle.isOn = PlayerPrefsGetBool(mPrefix + PREF_LOCALVIDEO, true);
    }

    private static bool PlayerPrefsGetBool(string name, bool defval)
    {
        int def = 0;
        if (defval)
            def = 1;
        return PlayerPrefs.GetInt(name, def) == 1 ? true : false;
    }

    private static void PlayerPrefsSetBool(string name, bool value)
    {
        PlayerPrefs.SetInt(name, value ? 1 : 0);
    }

    private string GetSelectedVideoDevice()
    {
        if (uVideoDropdown.value <= 0 || uVideoDropdown.value >= uVideoDropdown.options.Count)
        {
            return null;
        }
        else
        {
            string devname = uVideoDropdown.options[uVideoDropdown.value].text;
            return devname;
        }
    }

    private static int TryParseInt(string value, int defval)
    {
        int result;
        if (int.TryParse(value, out result) == false)
        {
            result = defval;
        }
        return result;
    }
    #endregion

    private void SetupCallApp()
    {
        mApp.SetVideoDevice(GetSelectedVideoDevice());
        mApp.SetAudio(uAudioToggle.isOn);
        mApp.SetVideo(uVideoToggle.isOn);

        int width = TryParseInt(uIdealWidth.text, 320);
        int height = TryParseInt(uIdealHeight.text, 240);
        int fps = TryParseInt(uIdealFps.text, 320);
        mApp.SetIdealResolution(width, height);
        mApp.SetIdealFps(fps);
        mApp.SetAutoRejoin(uRejoinToggle.isOn);
        mApp.SetShowLocalVideo(uLocalVideoToggle.isOn);
        mApp.SetupCall();
        EnsureLength();
        Append("Trying to listen on address " + uRoomNameInputField.text);
        mApp.Join(uRoomNameInputField.text);
    }

    #region ToggleButtons

    public void ToggleRenderTextureDisplay()
    {

    }

    //toggle audio on / off
    public void AudioToggle(bool state)
    {
        //moved. this is done during SetupCallApp
    }

    //toggle video on / off
    public void VideoToggle(bool state)
    {

    }

    public void ToggleSettings()
    {
        uMainSetupPanel.gameObject.SetActive(!uMainSetupPanel.gameObject.activeSelf);
        uSettingsPanel.gameObject.SetActive(!uSettingsPanel.gameObject.activeSelf);
    }

    public void ToggleSetup()
    {
        uSetupPanel.gameObject.SetActive(!uSetupPanel.gameObject.activeSelf);
    }

    //Join button pressed. Tries to join a room.
    public void JoinButtonPressed()
    {
        SaveSettings();
        SetupCallApp();
    }

    //This is called if the send button
    public void SendButtonPressed()
    {
        //get the message written into the text field
        string msg = uMessageInputField.text;
        SendMsg(msg);
    }

    //Shuts the network down
    public void ShutdownButtonPressed()
    {
        mApp.ResetCall();
    }

    public void OnVolumeChanged(float value)
    {
        mApp.SetRemoteVolume(value);
    }

    public void OnLoudspeakerToggle()
    {
        //watch out the on state of the toggle means
        //the icon is crossed out thus
        //isOn == true means the speaker is off
        bool state = !uLoudspeakerToggle.isOn;
        mApp.SetLoudspeakerStatus(state);
        //read if the state actually changed
        RefreshLoudspeakerToggle();
    }

    private void RefreshLoudspeakerToggle()
    {
        bool state = mApp.GetLoudspeakerStatus();
        uLoudspeakerToggle.isOn = !state;
    }

    public void OnMuteToggle()
    {
        bool state = uMuteToggle.isOn;
        mApp.SetMute(state);
        //read if the state actually changed
        RefreshMuteToggle();
    }

    private void RefreshMuteToggle()
    {
        bool state = mApp.IsMute();
        uMuteToggle.isOn = state;
    }

    //Updates the dropdown menu based on the current video devices and toggle status
    public void UpdateVideoDropdown()
    {
        uVideoDropdown.ClearOptions();
        uVideoDropdown.AddOptions(new List<string>(mApp.GetVideoDevices()));
        uVideoDropdown.interactable = mApp.CanSelectVideoDevice();

        //restore the stored selection if possible
        if (uVideoDropdown.interactable && mStoredVideoDevice != null)
        {
            int index = 0;
            foreach (var opt in uVideoDropdown.options)
            {
                if (opt.text == mStoredVideoDevice)
                {
                    uVideoDropdown.value = index;
                }
                index++;
            }
        }
    }
    public void VideoDropdownOnValueChanged(int index)
    {
        //moved to SetupCallApp
    }

    #endregion

    #region HandleStringMessages
    //User either pressed enter or left the text field
    //if return key was pressed send the message
    public void InputOnEndEdit()
    {
        if (Input.GetKey(KeyCode.Return))
        {
            string msg = uMessageInputField.text;
            SendMsg(msg);
        }
    }

    private void EnsureLength()
    {
        if (uRoomNameInputField.text.Length > VideoApp.MAX_CODE_LENGTH)
        {
            uRoomNameInputField.text = uRoomNameInputField.text.Substring(0, VideoApp.MAX_CODE_LENGTH);
        }
    }

    public string GetRoomname()
    {
        EnsureLength();
        return uRoomNameInputField.text;
    }

    //Sends a message to the other end
    private void SendMsg(string msg)
    {
        if (String.IsNullOrEmpty(msg))
        {
            //never send null or empty messages. webrtc can't deal with that
            return;
        }

        Append(msg);
        mApp.Send(msg);

        //reset UI
        uMessageInputField.text = "";
        uMessageInputField.Select();
    }


    //Adds a new message to the message view
    public void Append(string text)
    {
        if (uMessageOutput != null)
        {
            uMessageOutput.AddTextEntry(text);
        }
        Debug.Log("Chat output: " + text);
    }

    #endregion

    #region HandleDisplay

    private void SetFullscreen(bool value)
    {
        mFullscreen = value;
        if (mFullscreen)
        {
            uVideoPanel.SetParent(uVideoBaseFullscreen, false);
            uChatPanel.SetParent(uChatBaseFullscreen, false);
            uInCallBase.gameObject.SetActive(false);
            uFullscreenPanel.gameObject.SetActive(true);
        }
        else
        {
            uVideoPanel.GetComponent<RectTransform>().SetParent(uVideoBase, false);
            uChatPanel.GetComponent<RectTransform>().SetParent(uChatBase, false);
            uInCallBase.gameObject.SetActive(true);
            uFullscreenPanel.gameObject.SetActive(false);
        }
    }
    public void Fullscreen()
    {
        bool newValues = !mFullscreen;

        //just in case: make sure fullscreen button is ignored if in setup mode
        if (newValues == true && uSetupPanel.gameObject.activeSelf)
            return;
        SetFullscreen(newValues);

        transform.SetAsLastSibling();
    }

    public void ShowOverlay()
    {
        if (this.uVideoOverlay == null)
        {
            Debug.LogError("VideoOverlay transform is missing.");
            return;
        }
        if (this.uVideoOverlay.gameObject.activeSelf)
        {
            this.uVideoOverlay.gameObject.SetActive(false);
            mVideoOverlayTimeout = 0;
        }
        else
        {
            this.uVideoOverlay.gameObject.SetActive(true);
            mVideoOverlayTimeout = sDefaultOverlayTimeout;
        }
    }

    //Shows the setup screen or the chat + video
    public void SetGuiState(bool showSetup)
    {
        uSetupPanel.gameObject.SetActive(showSetup);

        uSendMessageButton.interactable = !showSetup;
        uShutdownButton.interactable = !showSetup;
        uMessageInputField.interactable = !showSetup;

        //this is going to hide the textures until it is updated with a new frame update
        UpdateLocalTexture(null, FramePixelFormat.Invalid);
        UpdateRemoteTexture(null, FramePixelFormat.Invalid);
        SetFullscreen(false);
    }

    #endregion

    #region UpdateFrames

    // Updates the local video. If the frame is null it will hide the video image
    public virtual void UpdateLocalTexture(IFrame frame, FramePixelFormat format)
    {
        if (uLocalVideoImage != null)
        {
            if (frame != null)
            {
                UnityMediaHelper.UpdateTexture(frame, ref mLocalVideoTexture);
                uLocalVideoImage.texture = mLocalVideoTexture;

                if (uLocalVideoImage.gameObject.activeSelf == false)
                {
                    uLocalVideoImage.gameObject.SetActive(true);
                }

                //watch out uLocalVideoImage should be scaled -1 X to make the local camera appear mirrored
                //it should also be scaled -1 Y because Unity reads the image from bottom to top
                uLocalVideoImage.transform.rotation = Quaternion.Euler(0, 0, frame.Rotation);

                mHasLocalVideo = true;
                mLocalFrameCounter++;
                mLocalVideoWidth = frame.Width;
                mLocalVideoHeight = frame.Height;
                mLocalVideoFormat = format;
            }
            else
            {
                //app shutdown. reset values
                mHasLocalVideo = false;
                uLocalVideoImage.texture = null;
                uLocalVideoImage.transform.rotation = Quaternion.Euler(0, 0, 0);
                uLocalVideoImage.gameObject.SetActive(false);
            }
        }
    }

    //Updates the remote video. If the frame is null it will hide the video image.
    public virtual void UpdateRemoteTexture(IFrame frame, FramePixelFormat format)
    {
        if (uRemoteVideoImage != null)
        {
            if (frame != null)
            {
                UnityMediaHelper.UpdateTexture(frame, ref mRemoteVideoTexture);
                uRemoteVideoImage.texture = mRemoteVideoTexture;

                if (debugMat != null)
                    debugMat.mainTexture = mRemoteVideoTexture;

                //watch out: due to conversion from WebRTC to Unity format the image is flipped (top to bottom)
                //this also inverts the rotation
                uRemoteVideoImage.transform.rotation = Quaternion.Euler(0, 0, frame.Rotation * -1);
                mHasRemoteVideo = true;
                mRemoteVideoWidth = frame.Width;
                mRemoteVideoHeight = frame.Height;
                mRemoteVideoFormat = format;
                mRemoteFrameCounter++;
            }
            else
            {
                mHasRemoteVideo = false;
                uRemoteVideoImage.texture = uNoCameraTexture;
                uRemoteVideoImage.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
        }
    }

    protected virtual void Update()
    {
        if(mVideoOverlayTimeout > 0)
        {
            string local = "Local:";
            if (mHasLocalVideo == false)
            {
                local += "no video";
            }
            else
            {
                local += mLocalVideoWidth + "x" + mLocalVideoHeight + Enum.GetName(typeof(FramePixelFormat), mLocalVideoFormat) + " FPS:" + mLocalFps;
            }
            string remote = "Remote:";
            if (mHasRemoteVideo == false)
            {
                remote += "no video";
            }
            else
            {
                remote += mRemoteVideoWidth + "x" + mRemoteVideoHeight + Enum.GetName(typeof(FramePixelFormat), mRemoteVideoFormat) + " FPS:" + mRemoteFps;
            }

            uOverlayInfo.text = local + "\n" + remote;
            mVideoOverlayTimeout -= Time.deltaTime;
            if(mVideoOverlayTimeout <= 0)
            {
                mVideoOverlayTimeout = 0;
                uVideoOverlay.gameObject.SetActive(false);
            }
        }

        float fpsTimeDif = Time.realtimeSinceStartup - mFpsTimer;
        if(fpsTimeDif > 1)
        {
            mLocalFps = Mathf.RoundToInt( mLocalFrameCounter / fpsTimeDif);
            mRemoteFps = Mathf.RoundToInt(mRemoteFrameCounter / fpsTimeDif);
            mFpsTimer = Time.realtimeSinceStartup;
            mLocalFrameCounter = 0;
            mRemoteFrameCounter = 0;
        }
    }
    #endregion

}
