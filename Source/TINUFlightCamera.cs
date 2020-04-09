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

	Quaternion deltaRotation;
	bool setRotation;

	void HandleInput ()
	{
		setRotation = false;
		if (!Input.GetMouseButton (cameraButton)) {
			return;
		}
		var center = new Vector3 (Screen.width / 2, Screen.height / 2, 0);
		Vector3 end = (Input.mousePosition - center) / Screen.width;
		end.z = -Screen.width / 2;
		end /= Screen.width / 2;
		float deltaMx = Input.GetAxis ("Mouse X");
		float deltaMy = Input.GetAxis ("Mouse Y");
		var delta = new Vector3 (deltaMx, deltaMy, 0) * orbitSensitivity;
		Vector3 start = end + delta;
		end = transform.TransformDirection (end);
		start = transform.TransformDirection (start);
		deltaRotation = Quaternion.FromToRotation (start, end);
		setRotation = true;
	}

	protected override void LateUpdate ()
	{
		Quaternion pivotRotation = cameraPivot.rotation;
		if (InputLockManager.IsUnlocked(ControlTypes.CAMERACONTROLS)
			|| (FlightDriver.Pause
				&& !KSP.UI.UIMasterController.Instance.IsUIShowing)) {
			HandleInput ();
		}
		//base.LateUpdate ();
		if (setRotation) {
			cameraPivot.rotation = deltaRotation * pivotRotation;
		} else {
			cameraPivot.rotation = pivotRotation;
		}
	}
}

}
