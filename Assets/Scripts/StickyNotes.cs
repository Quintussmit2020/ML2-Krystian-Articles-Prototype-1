using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.MagicLeap;

public class StickyNotes : MonoBehaviour
{
    public GameObject StickyNotePrefab;

    public float SearchInterval = 10;
    private Dictionary<string,GameObject> _persistentObjectsById = new Dictionary<string,GameObject>();

    private string _localizedSpace;

    private MagicLeapInputs _magicLeapInputs;
    private MagicLeapInputs.ControllerActions _controllerActions;

    private MLAnchors.Request _spatialAnchorRequest;

    //Used to force search localization even if the current time hasn't expired
    private bool _searchNow;
    //The timestamp when anchors were last searched for
    private float _lastTick;

    //The amount of searches that were performed.
    //Used to make sure anchors are fully localized before instantiating them.
    private int numberOfSearches;


    void Start()
    {
        SimpleAnchorBinding.Storage.LoadFromFile();

        _magicLeapInputs = new MagicLeapInputs();
        _magicLeapInputs.Enable();

        _controllerActions = new MagicLeapInputs.ControllerActions(_magicLeapInputs);
        _controllerActions.Bumper.started += Bumper_started;
        _controllerActions.Menu.started += Menu_started;
        _controllerActions.Trigger.started += TriggerStarted;

        var result = MLPermissions.CheckPermission(MLPermission.SpatialAnchors);

        if (result.IsOk)
        {
            MLResult mlResult = MLAnchors.GetLocalizationInfo(out MLAnchors.LocalizationInfo info);
#if !UNITY_EDITOR
            if (info.LocalizationStatus == MLAnchors.LocalizationStatus.NotLocalized)
            {
                UnityEngine.XR.MagicLeap.SettingsIntentsLauncher.LaunchSystemSettings("com.magicleap.intent.action.SELECT_SPACE");
            }
#endif

        }
    }

    public void SearchNow()
    {
        _searchNow = true;
    }

    private void LateUpdate()
    {
        // Only search when the update time lapsed 
        if (!_searchNow && Time.time - _lastTick < SearchInterval)
            return;

        _lastTick = Time.time;

        MLResult mlResult = MLAnchors.GetLocalizationInfo(out MLAnchors.LocalizationInfo info);
        if (!mlResult.IsOk)
        {
            Debug.Log("Could not get localization Info " + mlResult);
            return;
        }

        if (info.LocalizationStatus == MLAnchors.LocalizationStatus.NotLocalized)
        {
            //Clear the old visuals
            ClearVisuals();
            _localizedSpace = "";
            numberOfSearches = 0;
            Debug.Log("Not Localized " + info.LocalizationStatus);
            return;
        }

        //If we are in a new space or have not localized yet then try to localize
        if (info.SpaceId != _localizedSpace)
        {
            ClearVisuals();
            if (Localize())
            {
                _localizedSpace = info.SpaceId;
            }
        }
    }

    private void ClearVisuals()
    {
        foreach (var prefab in _persistentObjectsById.Values)
        {
            Destroy(prefab);
        }
        _persistentObjectsById.Clear();
    }

    private bool Localize()
    {
        MLResult startStatus = _spatialAnchorRequest.Start(new MLAnchors.Request.Params(Camera.main.transform.position, 100, 0, false));
        numberOfSearches++;

        if (!startStatus.IsOk)
        {
            Debug.LogError("Could not start" + startStatus);
            return false;
        }

        MLResult queryStatus = _spatialAnchorRequest.TryGetResult(out MLAnchors.Request.Result result);

        if (!queryStatus.IsOk)
        {
            Debug.LogError("Could not get result " + queryStatus);
            return false;
        }

        //Wait a search to make sure anchors are initialized
        if (numberOfSearches <= 1)
        {
            Debug.LogWarning("Initializing Anchors");
            //Search again
            _searchNow = true;
            return false;
        }

        for (int i = 0; i < result.anchors.Length; i++)
        {
            MLAnchors.Anchor anchor = result.anchors[i];
            var savedAnchor = SimpleAnchorBinding.Storage.Bindings.Find(x => x.Id == anchor.Id);
            if (savedAnchor != null && _persistentObjectsById.ContainsKey(anchor.Id) == false)
            {
                if (savedAnchor.JsonData == StickyNotePrefab.name)
                {
                    var persistentObject = Instantiate(StickyNotePrefab, anchor.Pose.position, anchor.Pose.rotation);
                    _persistentObjectsById.Add(anchor.Id, persistentObject);
                }
               
            }
        }

        return true;
    }

    private void Menu_started(InputAction.CallbackContext obj)
    {
     
    }

    private void TriggerStarted(InputAction.CallbackContext obj)
    {
        Pose controllerPose = new Pose(_controllerActions.Position.ReadValue<Vector3>(),
            _controllerActions.Rotation.ReadValue<Quaternion>());

        MLAnchors.Anchor.Create(controllerPose, 300, out MLAnchors.Anchor anchor);

        var result = anchor.Publish();
        if (result.IsOk)
        {
            SimpleAnchorBinding savedAnchor = new SimpleAnchorBinding();
            savedAnchor.Bind(anchor, StickyNotePrefab.name);
            var persistentObject = Instantiate(StickyNotePrefab, controllerPose.position, controllerPose.rotation);
            _persistentObjectsById.Add(anchor.Id, persistentObject);
            SimpleAnchorBinding.Storage.SaveToFile();
        }
    }

    private void Bumper_started(InputAction.CallbackContext obj)
    {
        //Request anchors near the controller's position
        MLAnchors.Request.Params requestParams =
            new MLAnchors.Request.Params(
               _controllerActions.Position.ReadValue<Vector3>(), 100, 0, true);

        // Start the search using the parameters specified in the Update function.
        MLResult startResult = _spatialAnchorRequest.Start(requestParams);

        if (!startResult.IsOk)
        {
            Debug.LogWarning("Anchor start error: " + startResult);
            return;
        }

        MLResult queryResult = _spatialAnchorRequest.TryGetResult(out MLAnchors.Request.Result result);
        if (!queryResult.IsOk)
        {
            Debug.LogWarning("Anchor query error: " + startResult);
            return;
        }

        // Get the search results.
        for (int i = 0; i < result.anchors.Length; i++)
        {
            // Get the closest anchor that we saved
            var anchor = result.anchors[i];
            if (RemoveAnchor(anchor.Id))
            {
                break;
            }
        }
    }


    //Returns true if the ID existed in the localized space and in the saved data
    private bool RemoveAnchor(string id)
    {
        //Delete the anchor using the Anchor's ID
        var savedAnchor = SimpleAnchorBinding.Storage.Bindings.Find(x => x.Id == id);
        //Delete the gameObject if it exists
        if (savedAnchor != null)
        {
            if (_persistentObjectsById.ContainsKey(id))
            {
                GameObject anchorVisual = _persistentObjectsById[id];
                _persistentObjectsById.Remove(id);
                Destroy(anchorVisual);
            }

            MLAnchors.Anchor.DeleteAnchorWithId(id);
            savedAnchor.UnBind();
            SimpleAnchorBinding.Storage.SaveToFile();
            return true;
        }

        return false;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
