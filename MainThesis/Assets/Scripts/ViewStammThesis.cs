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

    public RawImage _ImageCheck;

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
    public int _dataCaptureLimit;
    private int _camUpdateCount;

    public int _experimentNumber;
    public int _experimentType;

    private float _experimentTimer;
    public bool _hasExperimentStarted;
    public bool _hasExperimentEnded;
    public bool _captureHasStarted;

    public RenderTexture _historyBuffer;

    private bool _takeScreenshot;
    private RenderTexture _storeRT;
    private Camera cam;
    private IEnumerator captureLoop;
    private IEnumerator captureImage;

    [Header("Main Refs")]
    protected Texture2D _serverVideoTexture;
    public Animator _walkAnim;
    public bool _isAnimating;
    private Vector3 StartPos;
    private List<Texture2D> _storeTextureArray;
    private List<int> _storeIndex;
    private List<string> _storeSFPS;
    private List<string> _storeCFPS;
    private List<string> _storeRTT;
    public Transform PositionSender;

    [Header("Client Displays")]
    public RawImage _serverRawImageDisplay;
    public Material _serverMaterialDisplay;
    public GameObject _mobileTextureDisplay;
    public GameObject _rawImageDisplay;
    public Text _buttonAnimationText;

    [Header("Display Data UI")]
    public RectTransform _displayDataPanel;
    public Text _clientFPSText;
    public Text _serverFPSText;
    public Text _RTTText;
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
        _app = GetComponent<ControllerStammThesis>();
        _camUpdateCount = 0;
        _frameCount = 0;
        _frameCaptureCounter = 0;
        _hasExperimentStarted = false;
        _hasExperimentEnded = false;
        _takeScreenshot = false;
        _captureHasStarted = false;
        cam = Camera.main;

        if(_connectionType == ConnectionType.Client)
        {
            _buttonAnimationText.text = "Play";
            StartPos = _walkAnim.gameObject.transform.position;



            _storeTextureArray = new List<Texture2D>();
            _storeIndex = new List<int>();
            _storeSFPS = new List<string>();
            _storeCFPS = new List<string>();
            _storeRTT = new List<string>();

            int width = 1024;
            int height = 1024;

            _storeRT = new RenderTexture(width, height, 24);

            for (int i = 0; i < _dataCaptureLimit; i++)
            {
                Texture2D temp = new Texture2D(width, height);
                
                _storeTextureArray.Add(temp);
                _storeIndex.Add(i);
                _storeSFPS.Add("");
                _storeCFPS.Add("");
                _storeRTT.Add("");
            }
        }

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
        if(_connectionType == ConnectionType.Server)
        {
            SetDataPanelState(false);
        }
    }



    public void ResetButtonPressed()
    {
        _app.ResetConnection();
    }

    public void IncreaseSpeedfactorPressed()
    {
        if(_speedFactor >= 2.0f)
        {
            _speedFactor = 2.0f;
        }
        else
        {
            _speedFactor *= 10.0f;
            _speedFactor += 1.0f;
            _speedFactor *= .1f;
        }

        _deltaText.text = _speedFactor.ToString();
    }

    public void DecreaseSpeedfactorPressed()
    {
        if (_speedFactor <= 0.1f)
        {
            _speedFactor = 0.1f;
        }
        else
        {
            _speedFactor *= 10.0f;
            _speedFactor -= 1.0f;
            _speedFactor *= .1f;

        }

        _deltaText.text = _speedFactor.ToString();
    }

    private void SetDataPanelState(bool state)
    {
        _displayDataPanel.gameObject.SetActive(state);
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
        captureLoop = EnsureScreenCapture(1.0f);
        StartCoroutine(captureLoop);
    }

    private IEnumerator SingleScreenCapture(float time, int i)
    {
        _ImageCheck.texture = _storeTextureArray[i];

        byte[] bytes = _storeTextureArray[i].EncodeToPNG();

        _deltaText.text = "capture" + i.ToString();

        string pictureName = "f" + i.ToString() + ".png";
        NativeGallery.SaveImageToGallery(bytes, "StammThesisImages", pictureName, null);
        yield return new WaitForSeconds(time);
    }

    private IEnumerator EnsureScreenCapture(float time)
    {
        for (int i = 0; i < _frameCaptureCounter; i++)
        {
            captureImage = SingleScreenCapture(1.0f, i);
            StartCoroutine(captureImage);

            yield return new WaitForSeconds(time * 2);
        }

        captureImage = EnsureDataCapture(1.0f);
        StartCoroutine(captureImage);
        yield return new WaitForSeconds(time * 2);      
    }

    private IEnumerator EnsureDataCapture(float time)
    {
            string FILE_PATH = Application.persistentDataPath + "/EXPDATA.txt";
            if (File.Exists(FILE_PATH))
            {
                Debug.Log(FILE_PATH + " already exists.");
                _deltaText.text = "ALREADY DATA!";
                yield return new WaitForSeconds(time);
            }
            else
            {
            _deltaText.text = "Data Saved!";
            }
            StreamWriter sr = System.IO.File.CreateText(FILE_PATH);

            for (int i = 0; i < _dataCaptureLimit; i++)
            {
                sr.WriteLine(_storeIndex[i] + "," + _storeSFPS[i] + "," + _storeCFPS[i] + "," + _storeRTT[i]);
            }

            sr.Close();

        SetDataPanelState(true);
        yield return new WaitForSeconds(time);
    }

    public void CaptureFrameMobile()
    {
        //Texture2D capture = new Texture2D(_width, _height, TextureFormat.RGB24, false);
        RenderTexture.active = _storeRT;
        Graphics.Blit(_serverMaterialDisplay.mainTexture, _storeRT);

        _storeTextureArray[_frameCaptureCounter].ReadPixels(new Rect(0, 0, _storeRT.width, _storeRT.height), 0, 0);
        _storeTextureArray[_frameCaptureCounter].Apply();

        //_ImageCheck.texture = capture;

        //_storeTextureArray[_frameCaptureCounter] = capture;
        _storeIndex[_frameCaptureCounter] = _frameCaptureCounter;
        _storeSFPS[_frameCaptureCounter] = _serverFPSText.text;
        _storeCFPS[_frameCaptureCounter] = _clientFPSText.text;
        _storeRTT[_frameCaptureCounter] = _RTTText.text;

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
                _walkAnim.speed =  _speedFactor * .125f + .25f;
                _buttonAnimationText.text = "Stop";
                _walkAnim.gameObject.transform.position = StartPos;
                _experimentTimer = 0.0f;
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
                    //Graphics.Blit(_serverMaterialDisplay.mainTexture, _historyBuffer);
                    UnityMediaHelper.UpdateTexture(frame, ref _serverVideoTexture);

                    _serverMaterialDisplay.mainTexture = _serverVideoTexture;
                   // _serverMaterialDisplay.SetTexture("TexPrevious", _historyBuffer);

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
            _clientFPSText.text = (_frameCount / timeDif).ToString(); 

            _serverFPSText.text = (_serverFrameCounter / timeDif).ToString(); ;
        
            _RTTText.text = (RTTValue * 1000.0f).ToString();
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

    private Quaternion ByteArrayToRotation(byte[] bytes)
    {
        double x, y, z, w;
        x = BitConverter.ToDouble(bytes, 24);
        y = BitConverter.ToDouble(bytes, 32);
        z = BitConverter.ToDouble(bytes, 40);
        w = BitConverter.ToDouble(bytes, 48);
        return new Quaternion((float)x, (float)y, (float)z, (float)w);
    }

    private byte[] TransformToByteArray(Transform loc)
    {
        byte[] bytes = new byte[sizeof(double) * 7];
        Buffer.BlockCopy(BitConverter.GetBytes((double)loc.position.x), 0, bytes, 0 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)loc.position.y), 0, bytes, 1 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)loc.position.z), 0, bytes, 2 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)loc.rotation.x), 0, bytes, 3 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)loc.rotation.y), 0, bytes, 4 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)loc.rotation.z), 0, bytes, 5 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)loc.rotation.w), 0, bytes, 6 * sizeof(double), sizeof(double));
        return bytes;
    }

    private byte[] PositionToByteArray(Vector3 pos)
    {
        byte[] bytes = new byte[sizeof(double) * 3];
        Buffer.BlockCopy(BitConverter.GetBytes((double)pos.x), 0, bytes, 0 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)pos.y), 0, bytes, 1 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)pos.z), 0, bytes, 2 * sizeof(double), sizeof(double));
        return bytes;
    }

    private byte[] RotationToByteArray(Quaternion rot)
    {
        byte[] bytes = new byte[sizeof(double) * 4];
        Buffer.BlockCopy(BitConverter.GetBytes((double)rot.x), 0, bytes, 0 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)rot.y), 0, bytes, 1 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)rot.z), 0, bytes, 2 * sizeof(double), sizeof(double));
        Buffer.BlockCopy(BitConverter.GetBytes((double)rot.w), 0, bytes, 3 * sizeof(double), sizeof(double));
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
        _walkAnim.gameObject.transform.Translate(Vector3.forward * _speedFactor * Time.deltaTime * 2.0f);
        SendTransform(PositionSender);
        _camUpdateCount++;
    }

    public void SendTransform(Transform loc)
    {
        _app.SendTransformData(TransformToByteArray(loc));
        _roundTripTime = 0.0f;
    }

    public void HandlePosDataMessage(byte[] bytes)
    {
        Vector3 pos = ByteArrayToPosition(bytes);
        Quaternion rot = ByteArrayToRotation(bytes);
        if(_connectionType == ConnectionType.Server)
        {
            _camera.UpdateRemoteCamera(pos, rot);         
        }
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

        if (fpsTimeDif > 1 && _connectionType == ConnectionType.Client)
        {
            DisplayFrameData(fpsTimeDif, _roundTripTimeDisplay);

            _fpsTimer = Time.realtimeSinceStartup;
            _serverFrameCounter = 0;
            _frameCount = 0;
            _roundTripTimeDisplay = _roundTripTime;

            if(_experimentTimer > 32.0f && !_captureHasStarted)
            {
                _deltaText.text = "Start Capture!";
                _hasExperimentEnded = true;
                _captureHasStarted = true;
                CaptureFrameMobileAll();
            }
            else if(_experimentTimer > 2.0f && !_hasExperimentStarted)
            {
                //store index, time, sfps, cfps, rrt, frame
                _deltaText.text = "Exp begun!";
                _hasExperimentStarted = true;
            }
            else if (_experimentTimer > 0.0f && !_hasExperimentStarted)
            {
                SetDataPanelState(false);
            }
        }

        if (_isAnimating && _connectionType == ConnectionType.Client)
        {
            t3 = PositionSender.position;
            delta = CalculateDelta();

            //DisplayPos(t1, t3, delta);
            

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
            _experimentTimer += Time.deltaTime;


            if (_hasExperimentStarted && _frameCaptureCounter < _dataCaptureLimit && _camUpdateCount > _fps - 1)
            {
                CaptureFrameButtonPressed();
                _camUpdateCount = 0;
            }
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
