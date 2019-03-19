#if !UNITY_WEBGL || UNITY_EDITOR
using Byn.Media;
using Byn.Media.Native;
using UnityEngine;
using UnityEngine.UI;

    public class CameraStammThesis : MonoBehaviour
    {
        [Header("Camera References")]
        public Camera _Camera;

        public bool renderStereo = true;
        public float stereoSeparation = 0.064f;

        private float _previousSample;
        private Texture2D _texture;

        public bool _enableViewBypass;

        Vector3 delta;
        Quaternion deltaR;

        [Header("Texture References")]
        public RenderTexture _RTBuffer = null;
        public string _DeviceName = "RemoteCamera";
  
        public int _Fps;
        public int _Width;
        public int _Height;

        private string _inUseDeviceName;
        private byte[] _byteBuffer = null; 
        private NativeVideoInput _videoInput;

        public Vector3 _storeT1Pos;
        public Quaternion _storeT1Rot;
        public ViewStammThesis _uiRef;

        private bool _sendingBackData;

        private void Awake()
        {
            _inUseDeviceName = _DeviceName;
            _texture = new Texture2D(_Width, _Height, TextureFormat.ARGB32, false);


    }

        void Start()
        {
            _videoInput = UnityCallFactory.Instance.VideoInput;
            _videoInput.AddDevice(_inUseDeviceName, _Width, _Height, _Fps);
            _sendingBackData = false;
        }

        private void OnDestroy()
        {
            Destroy(_RTBuffer);
            Destroy(_texture);

            if (_videoInput != null)
                _videoInput.RemoveDevice(_inUseDeviceName);
        }

        public void UpdateRemoteCamera(Vector3 pos, Quaternion rot)
        {
            _storeT1Pos = _Camera.transform.position;
            _storeT1Rot = _Camera.transform.rotation;
            _Camera.transform.position = pos;
            _Camera.transform.rotation = rot;
             
            if (!_sendingBackData)
            {
                _sendingBackData = true;
            }
        }

    private void OnPreRender()
    {
        if (_enableViewBypass)
        {
            delta = _Camera.transform.position - _storeT1Pos;         
            Matrix4x4 m = Matrix4x4.TRS(delta, Quaternion.identity, new Vector3(1, 1, -1));
            _Camera.worldToCameraMatrix = m * transform.worldToLocalMatrix;
        }
        else
        {
            _Camera.ResetWorldToCameraMatrix();
        }
    }

    private void Update()
    {
        //ensure correct fps
        float deltaSample = 1.0f / _Fps;
        _previousSample += Time.deltaTime;
        if (_previousSample >= deltaSample)
        {
            _previousSample -= deltaSample;

            _Camera.targetTexture = _RTBuffer;
            _Camera.Render();

            if(RenderTexture.active != _RTBuffer)
            {
                RenderTexture.active = _RTBuffer;
            }
            

            _texture.ReadPixels(new Rect(0, 0, _RTBuffer.width, _RTBuffer.height), 0, 0, false);
            _texture.Apply();

            _byteBuffer = _texture.GetRawTextureData();

            _videoInput.UpdateFrame(_inUseDeviceName, _byteBuffer, _texture.width, _texture.height, WebRtcCSharp.VideoType.kBGRA, 0, true);
        }
    }

}


#endif
