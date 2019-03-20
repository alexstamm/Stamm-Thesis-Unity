using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Byn.Media;
using Byn.Media.Native;

public class StammThesis : MonoBehaviour {

    enum ConnectionType
    {
        Server,
        Client
    }

    enum TextureType
    {
        Cubemap,
        Equirectangular
    }

    enum DisplayType
    {
        Mono,
        Stereo
    }

    enum ProjectionType
    {
        Flat,
        Spherical
    }


    [Header("Experiment")]
    public bool IsClient;
    public float SpeedFactor;

    [Header("Main Refs")]
    protected Texture2D ServerVideoTexture;
    public RawImage ServerRawImageDisplay;
    public Material ServerMaterialDisplay;

    [Header("Display Data UI")]
    public RectTransform DisplayDataPanel;
    public Text FpsText;
    public Text LatText;
    public Text TimeText;

    [Header("Camera Texture Settings")]
    private float FpsTimer = 0;

    private int ServerVideoWidth = -1;
    private int ServerVideoHeight = -1;
    private int ServerFpsTimer = 0;
    private int ServerFrameCounter = 0;
    private FramePixelFormat ServerVideoFormat = FramePixelFormat.Invalid;




	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
