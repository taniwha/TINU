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
	}

	protected override void OnDestroy ()
	{
		base.OnDestroy ();
	}

	Quaternion deltaRotation;
	bool setRotation;

	const float r = 1;
	const float t = r * r / 2;
	// xyVec is already in -1..1 order of magnitude range (ie, might be a bit
	// over due to screen aspect)
	// This is very similar to blender's trackball calculation (based on it,
	// really)
	Vector3 TrackballVector (Vector3 xyVec)
	{
		float d = xyVec.x * xyVec.x + xyVec.y * xyVec.y;
		Vector3 vec = xyVec;

		if (d < t) {
			// less than 45 degrees around the sphere from the viewer facing
			// pole, so map the mouse point to the sphere
			vec.z = -Mathf.Sqrt (r * r - d);
		} else {
			// beyond 45 degrees around the sphere from the veiwer facing
			// pole, so the slope is rapidly approaching infinity or the mouse
			// point may miss the sphere entirely, so instead map the mouse
			// point to the hyperbolic cone pointed towards the viewer. The
			// cone and sphere are tangential at the 45 degree latitude
			vec.z = -t / Mathf.Sqrt (d);
		}
		return vec;
	}

	void CalcDragRotationDelta ()
	{
		var center = new Vector3 (Screen.width / 2, Screen.height / 2, 0);
		Vector3 end = (Input.mousePosition - center);
		end /= Screen.height / 2;
		float deltaMx = Input.GetAxis ("Mouse X");
		float deltaMy = Input.GetAxis ("Mouse Y");
		var delta = new Vector3 (deltaMx, deltaMy, 0) * orbitSensitivity;
		Vector3 start = end + delta;
		end = TrackballVector (end);
		start = TrackballVector (start);
		Vector3 axis = transform.TransformDirection(Vector3.Cross(start, end));
		float angle = delta.magnitude / (2 * r) * 60;
		deltaRotation = Quaternion.AngleAxis (angle, axis);
		setRotation = true;
	}

	void HandleInput ()
	{
		setRotation = false;
		if (Input.GetMouseButton (cameraButton)) {
			CalcDragRotationDelta ();
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
