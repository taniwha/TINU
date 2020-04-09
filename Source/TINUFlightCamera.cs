using System;
using System.Reflection;
using UnityEngine;

namespace TINU {

public class TINUFlightCamera : FlightCamera
{
	Transform cameraPivot;
	static int cameraButton = 1;

	protected override void Awake ()
	{
		base.Awake ();
	}

	protected override void Start ()
	{
		cameraPivot = transform.parent;
		Debug.Log($"[TINU] {cameraPivot.gameObject.name}");
	}

	protected override void OnDestroy ()
	{
		base.OnDestroy ();
	}

	Vector3 initialVector;
	Quaternion deltaRotation;
	bool setRotation;

	void HandleInput ()
	{
		if (Input.GetMouseButtonDown (cameraButton)) {
			var center = new Vector3 (Screen.width / 2, Screen.height / 2, 0);
			initialVector = Input.mousePosition - center;
			initialVector.x /= Screen.width / 2;
			initialVector.y /= Screen.height / 2;
			initialVector.z = -1;
			initialVector = transform.TransformDirection (initialVector);
		}
		setRotation = false;
		if (Input.GetMouseButton (cameraButton)) {
			float deltaMx = Input.GetAxis ("Mouse X");
			float deltaMy = Input.GetAxis ("Mouse Y");
			var delta = new Vector3 (deltaMx, deltaMy, 0);
			delta = transform.TransformDirection (delta);
			Vector3 newVector = initialVector - delta;
			deltaRotation = Quaternion.FromToRotation (initialVector, newVector);
			setRotation = true;
		}
	}

	protected override void LateUpdate ()
	{
		Quaternion pivotRotation = cameraPivot.rotation;
		if (InputLockManager.IsUnlocked(ControlTypes.CAMERACONTROLS)
			|| (FlightDriver.Pause
				&& !KSP.UI.UIMasterController.Instance.IsUIShowing)) {
			HandleInput ();
		}
		base.LateUpdate ();
		if (setRotation) {
			cameraPivot.rotation = deltaRotation * pivotRotation;
		} else {
			cameraPivot.rotation = pivotRotation;
		}
	}
}

}
