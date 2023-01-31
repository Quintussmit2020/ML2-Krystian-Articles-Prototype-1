using UnityEngine;
using UnityEngine.XR.MagicLeap;
public class HandTrackingInputExample : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        if (!MLPermissions.CheckPermission(MLPermission.HandTracking).IsOk)
        {
            Debug.LogError($"You must include the {MLPermission.HandTracking} permission in the AndroidManifest.xml to run this example.");
            enabled = false;
            XRIHandController[] handControllers = FindObjectsOfType<XRIHandController>();
            for (int i = 0; i < handControllers.Length; i++)
            {
                handControllers[i].gameObject.SetActive(false);
            }
            return;
        }

        InputSubsystem.Extensions.MLHandTracking.StartTracking();
        InputSubsystem.Extensions.MLGestureClassification.StartTracking();
    }
}
