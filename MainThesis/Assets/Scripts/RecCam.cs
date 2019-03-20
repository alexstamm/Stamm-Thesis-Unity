#if !UNITY_WEBGL || UNITY_EDITOR
using Byn.Media;
using Byn.Media.Native;
using UnityEngine;
using UnityEngine.UI;
namespace Byn.Unity.Examples
{
    /// <summary>
    /// Use at your own risk! The shown feature might not be stable
    /// and they are only tested on win64 so far.
    /// 
    /// Example for use of VideoInput class. 
    /// 
    /// This script will create a virtual video camera which then
    /// can be selected like a webcam via the CallApp.
    /// 
    /// It will obtain its image via a Unity Camera object and then store
    /// it in a native buffer. The image can be used later via the
    /// ICall or IMediaNetwork interface by setting
    /// MediaConfig.VideoDeviceName to the value of VirtualCamera._DeviceName.
    /// 
    /// Test the performance of this system first before using it. It might
    /// be too slow for many systems if used with high resolution images.
    /// 
    /// 
    /// </summary>
    public class RecCam : MonoBehaviour
    {
        public Camera _Camera;
        private float mLastSample;

        private Texture2D mTexture;

        public RenderTexture mRtBuffer = null;
        public RenderTexture mRtBufferEqui = null;

        /// <summary>
        /// Can be used to output the image sent for testing
        /// </summary>
        public RawImage _DebugTarget = null;


        /// <summary>
        /// Name used to access it later via MediaConfig
        /// </summary>
        public string _DeviceName = "VirtualCamera1";

        /// <summary>
        /// FPS the virtual device is suppose to have.
        /// (This isn't really used yet except to filter
        /// out this device if MediaConfig requests specific FPS)
        /// </summary>
        public int _Fps = 60;

        /// <summary>
        /// Width the output is suppose to have
        /// </summary>
        public int _Width = 640;
        /// <summary>
        /// Height the output is suppose to have
        /// </summary>
        public int _Height = 480;

        private string mUsedDeviceName;
        private byte[] mByteBuffer = null;

        /// <summary>
        /// Interface for video device input.
        /// </summary>
        private NativeVideoInput mVideoInput;

        [Header("EquiRect Refs")]
        public RenderTexture cubemapLeft;
        public RenderTexture cubemapRight;
        public bool renderStereo = true;
        public float stereoSeparation = 0.064f;

        private void Awake()
        {
            mUsedDeviceName = _DeviceName;
            //mRtBuffer = new RenderTexture(1024/*_Width*/, 1024/*_Height*/, 0, RenderTextureFormat.ARGB32);
            //mRtBufferEqui = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);

            //mRtBuffer.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            //mRtBuffer.wrapMode = TextureWrapMode.Clamp;

            //mRtBufferEqui.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            //mRtBufferEqui.wrapMode = TextureWrapMode.Clamp; 

            mTexture = new Texture2D(_Width, _Height, TextureFormat.ARGB32, false);

        }

        // Use this for initialization
        void Start()
        {
            mVideoInput = UnityCallFactory.Instance.VideoInput;
            mVideoInput.AddDevice(mUsedDeviceName, _Width, _Height, _Fps);
        }

        private void OnDestroy()
        {
            Destroy(mRtBuffer);
            Destroy(mTexture);

            if (mVideoInput != null)
                mVideoInput.RemoveDevice(mUsedDeviceName);
        }
        

        void Update()
        {
            //ensure correct fps
            float deltaSample = 1.0f / _Fps;
            mLastSample += Time.deltaTime;
            if (mLastSample >= deltaSample)
            {
                mLastSample -= deltaSample;

                //backup the current configuration to restore it later
                var oldTargetTexture = _Camera.targetTexture;
                var oldActiveTexture = RenderTexture.active;

                //Set the buffer as target and render the view of the camera into it
                //_Camera.targetTexture = mRtBuffer;
                //_Camera.Render();

                if (renderStereo)
                {
                    _Camera.stereoSeparation = stereoSeparation;

                    _Camera.RenderToCubemap(cubemapLeft, 63, Camera.MonoOrStereoscopicEye.Left);
                    _Camera.RenderToCubemap(cubemapRight, 63, Camera.MonoOrStereoscopicEye.Right);

                    cubemapLeft.ConvertToEquirect(mRtBufferEqui, Camera.MonoOrStereoscopicEye.Left);
                    cubemapRight.ConvertToEquirect(mRtBufferEqui, Camera.MonoOrStereoscopicEye.Right);
                }
                else
                {
                    _Camera.RenderToCubemap(mRtBuffer, 63, Camera.MonoOrStereoscopicEye.Mono);

                    mRtBuffer.ConvertToEquirect(mRtBufferEqui, Camera.MonoOrStereoscopicEye.Mono);
                }

                RenderTexture.active = mRtBufferEqui;
                mTexture.ReadPixels(new Rect(0, 0, mRtBufferEqui.width, mRtBufferEqui.height), 0, 0, false);
                mTexture.Apply();

                //get the byte array. still looking for a way to reuse the current buffer
                //instead of allocating a new one all the time
                mByteBuffer = mTexture.GetRawTextureData();


                //update the internal WebRTC device
                mVideoInput.UpdateFrame(mUsedDeviceName, mByteBuffer, mTexture.width, mTexture.height, WebRtcCSharp.VideoType.kBGRA, 0, true);

                //reset the camera/active render texture  in case it is still used for other purposes
                _Camera.targetTexture = oldTargetTexture;
                RenderTexture.active = oldActiveTexture;

                //update debug output if available
                if (_DebugTarget != null)
                    _DebugTarget.texture = mTexture;

            }
        }
    }

}

#endif
