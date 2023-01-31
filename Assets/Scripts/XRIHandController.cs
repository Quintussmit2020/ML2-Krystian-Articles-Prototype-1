using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.MagicLeap;

public class XRIHandController : ActionBasedController
{
    [SerializeField,
     Tooltip("The XRNode associated with this Hand Controller. Expected to be XRNode.LeftHand or XRNode.RightHand.")]
    private XRNode handNode = XRNode.LeftHand;

    /// <summary>
    /// The XRNode associated with this Hand Controller.
    /// </summary>
    /// <remarks>Expected to be XRNode.LeftHand or XRNode.RightHand.</remarks>
    public XRNode HandNode => handNode;

    private InputDevice handDevice;
    private InputDevice gestureDevice;

    private bool pinchedLastFrame = false;

    private List<Bone> _pinkyFingerBones = new List<Bone>();
    private List<Bone> _ringFingerBones = new List<Bone>();
    private List<Bone> _middleFingerBones = new List<Bone>();
    private List<Bone> _indexFingerBones = new List<Bone>();
    private List<Bone> _thumbBones = new List<Bone>();
    protected override void Awake()
    {
        base.Awake();
        currentControllerState = new XRControllerState();
    }

    protected override void UpdateInput(XRControllerState controllerState)
    {
        if (!handDevice.isValid)
        {
            InputDeviceCharacteristics handedness =
                handNode == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
            handDevice = InputSubsystem.Utils.FindMagicLeapDevice(InputDeviceCharacteristics.HandTracking | handedness);
        }

        base.UpdateInput(controllerState);

        if (controllerState == null)
            return;

        bool isPinched = GetPinch();

        controllerState.selectInteractionState.active = isPinched;
        controllerState.selectInteractionState.activatedThisFrame = isPinched && !pinchedLastFrame;
        controllerState.selectInteractionState.deactivatedThisFrame = !isPinched && pinchedLastFrame;
        pinchedLastFrame = isPinched;
    }

    protected override void UpdateTrackingInput(XRControllerState controllerState)
    {
        if (!handDevice.isValid)
        {
            InputDeviceCharacteristics handedness =
                handNode == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
            handDevice = InputSubsystem.Utils.FindMagicLeapDevice(InputDeviceCharacteristics.HandTracking | handedness);
        }

        if (!handDevice.isValid)
            controllerState.inputTrackingState = InputTrackingState.None;

        base.UpdateTrackingInput(controllerState);

        var pose = UpdateHandRay();

        controllerState.position = pose.position;
        controllerState.rotation = pose.rotation;
        controllerState.inputTrackingState = InputTrackingState.Position | InputTrackingState.Rotation;
    }
    protected Pose UpdateHandRay()
    {
        Pose rayPose = new Pose();

        if (handDevice.isValid && handDevice.TryGetFeatureValue(CommonUsages.handData, out UnityEngine.XR.Hand hand))
        {
            hand.TryGetFingerBones(UnityEngine.XR.HandFinger.Index, this._indexFingerBones);
            hand.TryGetFingerBones(UnityEngine.XR.HandFinger.Middle, this._middleFingerBones);
            hand.TryGetFingerBones(UnityEngine.XR.HandFinger.Ring, this._ringFingerBones);
            hand.TryGetFingerBones(UnityEngine.XR.HandFinger.Pinky, this._pinkyFingerBones);
            hand.TryGetFingerBones(UnityEngine.XR.HandFinger.Thumb, this._thumbBones);

            _indexFingerBones[_indexFingerBones.Count - 1].TryGetPosition(out Vector3 indexPosition);
            rayPose.position = indexPosition;

            handDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion deviceRotation);

            //Pointer Rotation
            Camera mainCam = Camera.main;
            float extraRayRotationX = -20.0f;
            float extraRayRotationY = 25.0f * ((handNode == XRNode.LeftHand) ? 1.0f : -1.0f);

            Quaternion targetRotation = Quaternion.LookRotation(rayPose.position - mainCam.transform.position, deviceRotation * Vector3.forward);
            Vector3 euler = targetRotation.eulerAngles + new Vector3(extraRayRotationX, extraRayRotationY, 0.0f);
            rayPose.rotation = Quaternion.Euler(euler);
        }

        return rayPose;
    }
    private bool GetPinch()
    {
        if (!gestureDevice.isValid)
        {
            List<InputDevice> foundDevices = new List<InputDevice>();
            InputDevices.GetDevices(foundDevices);
            string gestureDeviceName = handNode == XRNode.LeftHand
                ? InputSubsystem.Extensions.MLGestureClassification.LeftGestureInputDeviceName
                : InputSubsystem.Extensions.MLGestureClassification.RightGestureInputDeviceName;

            foreach (InputDevice device in foundDevices)
            {
                if (device.name == gestureDeviceName)
                {
                    gestureDevice = device;
                    break;
                }
            }
            return false;
        }

        InputSubsystem.Extensions.MLGestureClassification.TryGetHandPosture(gestureDevice, out InputSubsystem.Extensions.MLGestureClassification.PostureType leftPosture);
        return leftPosture == InputSubsystem.Extensions.MLGestureClassification.PostureType.Pinch;
    }

}
