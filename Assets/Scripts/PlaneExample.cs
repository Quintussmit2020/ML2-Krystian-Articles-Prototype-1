using System;
using System.Collections;
using System.Collections.Generic;
using MagicLeap.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.MagicLeap;

public class PlaneExample : MonoBehaviour
{
    private ARPlaneManager planeManager;

    [SerializeField, Tooltip("Maximum number of planes to return each query")]
    private uint maxResults = 100;

    [SerializeField, Tooltip("Minimum hole length to treat as a hole in the plane")]
    private float minHoleLength = 0.5f;

    [SerializeField, Tooltip("Minimum plane area to treat as a valid plane")]
    private float minPlaneArea = 0.25f;

    private MagicLeapInputs magicLeapInputs;
    private MagicLeapInputs.ControllerActions controllerActions;

    private readonly MLPermissions.Callbacks permissionCallbacks = new MLPermissions.Callbacks();

    public Transform mediaPlayerRoot;
    public MLMediaPlayerBehavior mlMediaPlayer;
    public GameObject mediaPlayerIndicator;

    private bool isPlacing = true;

    public GameObject screenDimmer;

    public MLVoiceIntentsConfiguration VoiceIntentsConfiguration;

    private void Awake()
    {
        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;
    }

    private void OnDestroy()
    {
        permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied -= OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain -= OnPermissionDenied;
    }

    private void Start()
    {
        planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.LogError("Failed to find ARPlaneManager in scene. Disabling Script");
            enabled = false;
        }
        else
        {
            // disable planeManager until we have successfully requested required permissions
            planeManager.enabled = false;
        }

        MLPermissions.RequestPermission(MLPermission.SpatialMapping, permissionCallbacks);
        MLPermissions.RequestPermission(MLPermission.VoiceInput, permissionCallbacks);

        mediaPlayerIndicator.SetActive(false);
        mediaPlayerRoot.gameObject.SetActive(false);

        magicLeapInputs = new MagicLeapInputs();
        magicLeapInputs.Enable();
        controllerActions = new MagicLeapInputs.ControllerActions(magicLeapInputs);
        controllerActions.Trigger.performed += Trigger_performed;
        controllerActions.TouchpadPosition.performed += TouchpadPositionOnperformed;

        MLSegmentedDimmer.Activate();

    }

    private void TouchpadPositionOnperformed(InputAction.CallbackContext obj)
    {
        var touchPosition = controllerActions.TouchpadPosition.ReadValue<Vector2>();
        var DimmingValue = Mathf.Clamp((touchPosition.y + 1) / (1.8f), 0, 1);
        screenDimmer.GetComponent<MeshRenderer>().material.SetFloat("_DimmingValue", DimmingValue);
        Debug.Log(DimmingValue);
    }

    private void Trigger_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    {
        Debug.Log("Trigger pressed");
        
        if (mediaPlayerIndicator.activeSelf)
        {
            isPlacing = false;
            mediaPlayerRoot.gameObject.SetActive(true);
            mediaPlayerIndicator.SetActive(false);
            mediaPlayerRoot.transform.position = mediaPlayerIndicator.transform.position;
            mediaPlayerRoot.transform.rotation = mediaPlayerIndicator.transform.rotation;
            mlMediaPlayer.Play();
        }
  
    }

    private void Update()
    {
        if (planeManager.enabled)
        {
            Debug.Log("Activated");
            PlanesSubsystem.Extensions.Query = new PlanesSubsystem.Extensions.PlanesQuery
            {
                Flags = planeManager.requestedDetectionMode.ToMLQueryFlags() | PlanesSubsystem.Extensions.MLPlanesQueryFlags.Polygons | PlanesSubsystem.Extensions.MLPlanesQueryFlags.Semantic_Wall,
                BoundsCenter = Camera.main.transform.position,
                BoundsRotation = Camera.main.transform.rotation,
                BoundsExtents = Vector3.one * 20f,
                MaxResults = maxResults,
                //MinHoleLength = minHoleLength,
                MinPlaneArea = minPlaneArea
            };
        }
        
        Ray raycastRay = new Ray(controllerActions.Position.ReadValue<Vector3>(), controllerActions.Rotation.ReadValue<Quaternion>() * Vector3.forward);
        if (isPlacing & Physics.Raycast(raycastRay, out RaycastHit hitInfo, 100, LayerMask.GetMask("Planes")))
        {
            Debug.Log(hitInfo.transform);
            mediaPlayerIndicator.transform.position = hitInfo.point;
            mediaPlayerIndicator.transform.rotation = Quaternion.LookRotation(-hitInfo.normal);
            mediaPlayerIndicator.gameObject.SetActive(true);
         
        }
    }

    public void ExitMediaPlayer()
    {
        mlMediaPlayer.Pause();
        mediaPlayerRoot.gameObject.SetActive(false);
        mediaPlayerIndicator.SetActive(true);
        isPlacing = true;
    }

    private void OnPermissionGranted(string permission)
    {
        if(permission == MLPermission.SpatialMapping)
            planeManager.enabled = true;

        if (permission == MLPermission.VoiceInput)
            InitializeVoiceInput();
    }

    private void InitializeVoiceInput()
    {
        bool isVoiceEnabled = MLVoice.VoiceEnabled;
        if (isVoiceEnabled)
        {
           var result = MLVoice.SetupVoiceIntents(VoiceIntentsConfiguration);
           if (result.IsOk)
           {
                MLVoice.OnVoiceEvent += MLVoiceOnOnVoiceEvent;
           }
           else
           {
               Debug.LogError("Voice could not initialize:" + result);
           }
        }
        else
        {
            UnityEngine.XR.MagicLeap.SettingsIntentsLauncher.LaunchSystemVoiceInputSettings();
            Application.Quit();
        }
    }


    private void MLVoiceOnOnVoiceEvent(in bool wassuccessful, in MLVoice.IntentEvent voiceevent)
    {
        if (wassuccessful)
        {
            if (voiceevent.EventID == 101)
            {
                Debug.Log("Show Global Dimmer");
                ToggleGlobalDimming(true);
            }
            if (voiceevent.EventID == 102)
            {
                Debug.Log("Hide Global Dimmer");
                ToggleGlobalDimming(false);
            }
            if (voiceevent.EventID == 103)
            {
                Debug.Log("Show Segmented Dimmer");
                screenDimmer.SetActive(true);
            }
            if (voiceevent.EventID == 104)
            {
                Debug.Log("Hide Segmented Dimmer");
                screenDimmer.SetActive(false);
            }
        }
    }

    private void ToggleGlobalDimming(bool isEnabled)
    {
        MLGlobalDimmer.SetValue(isEnabled ? 1 : 0);
    }

    private void OnPermissionDenied(string permission)
    {
        Debug.LogError($"Failed to create Planes Subsystem due to missing or denied {MLPermission.SpatialMapping} permission. Please add to manifest. Disabling script.");
        enabled = false;
    }

}
