using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR;
using Byn.Media;
using Byn.Media.Native;

public class ViewStammThesis : MonoBehaviour {

    //TODO: use for toggling states in build
    public enum ConnectionType
    {
        Server,
        Client
    }

    public enum TextureType
    {
        Cubemap,
        Equirectangular
    }

    public enum DisplayType
    {
        Mono,
        Stereo
    }

    public enum ProjectionType
    {
        Flat,
        Spherical
    }


    private Vector3 t1;
    private Vector3 t3;
    private Vector3 delta;
    private bool _isRenderingDepthTexture;

    [Header("Experiment")]
    public ConnectionType _connectionType;
    public TextureType _textureType;
    public DisplayType _displayType;
    public ProjectionType _projectionType;
    public float _speedFactor;
    public int _width;
    public int _height;
    public int _fps;
    public float _roundTripTime;
    public float _roundTripTimeDisplay;
    public int _frameCaptureRate;
    public int _frameCaptureTotal;
    private int _frameCaptureCounter;
    private int _frameCount;

    public RenderTexture _historyBuffer;

    private bool _takeScreenshot;
    private RenderTexture _storeRT;
    private Camera cam;
    private IEnumerator captureLoop;

    private Matrix4x4 _leftViewMatrixInv;
    private Matrix4x4 _rightViewMatrixInv;
    private Matrix4x4 _leftProjMatrixInv;
    private Matrix4x4 _rightProjMatrixInv;

    [Header("Main Refs")]
    protected Texture2D _serverVideoTexture;
    protected Texture _previousVideoTexture;
    public Animator _walkAnim;
    public bool _isAnimating;
    private Vector3 StartPos;
    private List<Texture2D> _storeTextureArray;
    public Transform PositionSender;

    [Header("Client Displays")]
    public RawImage _serverRawImageDisplay;
    public Material _serverMaterialDisplay;
    public GameObject _mobileTextureDisplay;
    public GameObject _rawImageDisplay;
    public Text _buttonAnimationText;

    [Header("Display Data UI")]
    public RectTransform _displayDataPanel;
    public Text _fpsText;
    public Text _latText;
    public Text _timeText;
    public Text _t1Text;
    public Text _t3Text;
    public Text _deltaText;

    [Header("Camera Texture Settings")]
    private float _fpsTimer = 0;

    private int _serverVideoWidth = -1;
    private int _serverVideoHeight = -1;
    private int _serverFpsTimer = 0;
    private int _serverFrameCounter = 0;
    private FramePixelFormat _serverVideoFormat = FramePixelFormat.Invalid;

    protected ControllerStammThesis _app;
    public CameraStammThesis _camera;

    #region InitFunctions
    protected virtual void Awake()
    {
        _frameCount = 0;
        _frameCaptureCounter = 0;
        

        cam = Camera.main;

        _takeScreenshot = false;

        _app = GetComponent<ControllerStammThesis>();
    

        if(_connectionType == ConnectionType.Client)
        {
            _buttonAnimationText.text = "Play";
            StartPos = _walkAnim.gameObject.transform.position;

            _storeTextureArray = new List<Texture2D>();

            int width = 1024;
            int height = 1024;

            _storeRT = new RenderTexture(width, height, 24);

            for (int i = 0; i < 10; i++)
            {
                Texture2D temp = new Texture2D(width, height);
                _storeTextureArray.Add(temp);
            }
        }

        //SetProjectionType(_projectionType);

        UpdateDisplayTexture(null, FramePixelFormat.Invalid);
    }

    protected virtual void Start()
    {
        if(this._displayDataPanel != null)
        {
            this._displayDataPanel.gameObject.SetActive(true);
        }

        if (!_isAnimating)
        {
            _isAnimating = true;
        }

        ResetAnimation();
    }

    private void SetVideoSettings(DisplayType displayType, TextureType textureType, ProjectionType projectionType)
    {
        _displayType = displayType;
        _textureType = textureType;
        _projectionType = projectionType;              
    }

    private DisplayType GetDisplayType()
    {
        return _displayType;
    }

    private TextureType GetTextureType()
    {
        return _textureType;
    }

    private ProjectionType GetProjectionType()
    {
        return _projectionType;
    }

    private void SetupVideoConnection()
    {
        _app.SetVideo(true);

        _app.SetIdealResolution(_width, _height);
        _app.SetIdealFps(_fps);
        _app.SetupConnection();

        // both connect to string name 'c' in this context
        _app.Join("c");
    }
    #endregion

    #region Buttons
    public void JoinButtonPressed()
    {
        SetupVideoConnection();
    }

    public void ResetButtonPressed()
    {
        _app.ResetConnection();
    }

    public void SwapProjectionType()
    {
        if (_mobileTextureDisplay.activeSelf)
        {
            _mobileTextureDisplay.SetActive(false);
            _serverRawImageDisplay.gameObject.SetActive(true);
            _projectionType = ProjectionType.Flat;
        }
        else
        {
            _mobileTextureDisplay.SetActive(true);
            _serverRawImageDisplay.gameObject.SetActive(false);
            _projectionType = ProjectionType.Spherical;
        }

    }

    public void CaptureFrameMobileAll()
    {
        for(int i = 0; i < _frameCaptureCounter; i++)
        {
            captureLoop = EnsureScreenCapture(1.0f, i);
            StartCoroutine(captureLoop);
        }

    }

    private IEnumerator EnsureScreenCapture(float time, int i)
    {
        byte[] bytes = _storeTextureArray[i].EncodeToPNG();

        string pictureName = "Exp:" + i + ".png";
        NativeGallery.SaveImageToGallery(bytes, "StammThesisImages", pictureName, null);
        yield return new WaitForSeconds(time);
    }

    public void CaptureFrameMobile()
    {

        int width = _serverMaterialDisplay.mainTexture.width;
        int height = _serverMaterialDisplay.mainTexture.height;

        cam.targetTexture = _storeRT;
        Texture2D capture = new Texture2D(width, height, TextureFormat.RGB24, false);
        cam.Render();
        RenderTexture.active = _storeRT;
        capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _storeTextureArray[_frameCaptureCounter] = capture;

        cam.targetTexture = null;
        RenderTexture.active = null;

        _frameCaptureCounter++;
        _takeScreenshot = false;

    }

    public void CaptureFrameButtonPressed()
    {
        _takeScreenshot = true;
    }

    public void ShowDepthTextureButtonPressed()
    {
        if (_isRenderingDepthTexture)
        {
            _isRenderingDepthTexture = false;
        }
        else
        {
            _isRenderingDepthTexture = true;
        }
    }

    public void ResetAnimation()
    {
        if(_connectionType == ConnectionType.Client)
        {           
            if (!_isAnimating)
            {
                _isAnimating = true;
                _walkAnim.Play("EntryBuffer", 0, 0.0f);
                _walkAnim.speed = _speedFactor * .5f;
                _buttonAnimationText.text = "Stop";
                _walkAnim.gameObject.transform.position = StartPos;                
            }
            else
            {
                _isAnimating = false;              
                _walkAnim.speed = 0.0f;
                _buttonAnimationText.text = "Play";
            }
        }      
    }

    #endregion

    #region HandleDisplay

    public void ShowDataPanel(bool state)
    {
        if(_connectionType == ConnectionType.Client)
        {
            if (this._displayDataPanel == null)
            {
                Debug.LogError("missing data panel");
                return;
            }
            else if (state)
            {
                this._displayDataPanel.gameObject.SetActive(true);
            }
            else
            {
                this._displayDataPanel.gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region FrameCalls

    public virtual void UpdateDisplayTexture(IFrame frame, FramePixelFormat format)
    {
        if(_connectionType == ConnectionType.Client)
        {
            if (frame != null)
            {
                if (_projectionType == ProjectionType.Flat)
                {
                    UnityMediaHelper.UpdateTexture(frame, ref _serverVideoTexture);
                    _serverRawImageDisplay.texture = _serverVideoTexture;
                    _serverRawImageDisplay.transform.rotation = Quaternion.Euler(0, 0, frame.Rotation);
                    _serverFrameCounter++;
                    _serverVideoWidth = frame.Width;
                    _serverVideoHeight = frame.Height;
                    _serverVideoFormat = format;
                }
                else if (_projectionType == ProjectionType.Spherical)
                {
                    Graphics.Blit(_serverMaterialDisplay.mainTexture, _historyBuffer);
                    UnityMediaHelper.UpdateTexture(frame, ref _serverVideoTexture);

                    _serverMaterialDisplay.mainTexture = _serverVideoTexture;
                    _serverMaterialDisplay.SetTexture("TexPrevious", _historyBuffer);


                    _serverVideoWidth = frame.Width;
                    _serverVideoHeight = frame.Height;
                    _serverVideoFormat = format;
                    _serverFrameCounter++;
                }

            }
            else
            {
                _serverRawImageDisplay.texture = null;
                _serverRawImageDisplay.transform.rotation = Quaternion.Euler(0, 0, 0);

                _serverMaterialDisplay.SetTexture("Texture", null);
            }
        }
    }

    private void DisplayFrameData(float timeDif, float RTTValue)
    {
        if(_connectionType == ConnectionType.Client)
        {
            _fpsText.text = "Mobile FPS: " + Mathf.RoundToInt(_frameCount / timeDif).ToString(); 

            _latText.text = "Server FPS: " + Mathf.RoundToInt(_serverFrameCounter / timeDif).ToString(); ;
        
            _timeText.text = "Ping: + " + (RTTValue * 1000.0f).ToString();
            // _timeText.text = "Count: " + _frameCount.ToString();
        }   
    }

    private Vector3 ByteArrayToPosition(byte[] bytes)
    {
        double x, y, z;
        x = BitConverter.ToDouble(bytes, 0);
        y = BitConverter.ToDouble(bytes, 8);
        z = BitConverter.ToDouble(bytes, 16);
        return new Vector3((float)x, (float)y, (float)z);
    }

    private byte[] PositionToByteArray(Vector3 pos)
    {
        byte[] bytes = new byte[sizeof(double) * 3];
        Buffer.BlockCopy(BitConverter.GetBytes((double)pos.x), 0, bytes, 0 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)pos.y), 0, bytes, 1 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)pos.z), 0, bytes, 2 * sizeof(double), sizeof(double));
        return bytes;
    }

    public void DisplayReceivedPos(string s)
    {
        if (_connectionType == ConnectionType.Client)
        {
            _t1Text.text = s;
        }
    }

    private void DisplayPos(Vector3 t1, Vector3 t3, Vector3 delta)
    {
        if(_connectionType == ConnectionType.Client)
        {
            _t1Text.text = "t1: " + t1.ToString();
            _t3Text.text = "t3: " + t3.ToString();
            _deltaText.text = "delta: " + delta.ToString();            
        }
    }

    private void UpdateCameraController()
    {
        _walkAnim.gameObject.transform.Translate(Vector3.forward * _speedFactor * Time.deltaTime * 5.0f);

        SendPos(PositionSender.position);
    }

    public void SendPosButtonPressed()
    {
        SendPos(PositionSender.position);        
    }

    public void SendPos(Vector3 pos)
    {      
        _app.SendPosData(PositionToByteArray(pos));
        _roundTripTime = 0.0f;
    }

    private void SetT1(Vector3 t)
    {
        if (_connectionType == ConnectionType.Client) //doesnt work because camera stamm thesis is only connected to the client
                                                      //need to send message from server camera script to client view
        {
            t1 = t; //this is set by camera, but it needs to be sent over the network over same way it came
        }
    }

    public void HandlePosDataMessage(byte[] bytes)
    {
        Vector3 pos = ByteArrayToPosition(bytes);
        if(_connectionType == ConnectionType.Server)
        {
            _camera.UpdateRemoteCamera(pos);         
        }
        else
        {
            SetT1(pos);
        }
    }

    public void WriteDataToFile()
    {
        string FILE_PATH = Application.persistentDataPath + "/TESTDATAOUTPUT.txt";
        if (File.Exists(FILE_PATH))
        {
            Debug.Log(FILE_PATH + " already exists.");
            return;
        }
        StreamWriter sr = System.IO.File.CreateText(FILE_PATH);
        sr.WriteLine("mobile FPS: " + _fpsText.text);
        sr.WriteLine("server FPS: " + _latText.text);
        sr.WriteLine("Ping: " + _timeText.text);
        sr.Close();
    }

    private Vector3 CalculateDelta()
    {
        return t3 - t1;
    }

    private float CalculateFrameLatency()
    {
        return Time.deltaTime;
    }

    protected virtual void Update()
    {
        float fpsTimeDif = Time.realtimeSinceStartup - _fpsTimer;

        _roundTripTime += Time.deltaTime;

        if (fpsTimeDif > 1)
        {
            _fpsTimer = Time.realtimeSinceStartup;
            _serverFrameCounter = 0;
            _frameCount = 0;
            _roundTripTimeDisplay = _roundTripTime;
        }

        if (_isAnimating && _connectionType == ConnectionType.Client)
        {
            t3 = PositionSender.position;
            delta = CalculateDelta();

            DisplayPos(t1, t3, delta);
            DisplayFrameData(fpsTimeDif, _roundTripTimeDisplay);

            if (_isRenderingDepthTexture)
            {
                _serverMaterialDisplay.SetFloat("_ShowMotion", 1.0f);
            }
            else
            {
                _serverMaterialDisplay.SetFloat("_ShowMotion", 0.0f);
            }
        }

        _frameCount++;


    }

    private void FixedUpdate()
    {
        if (_isAnimating && _connectionType == ConnectionType.Client)
        {
            UpdateCameraController();
        }
    }

    private void LateUpdate()
    {
        if (_takeScreenshot)
        {
            CaptureFrameMobile();
        }

    }

    #endregion

}
