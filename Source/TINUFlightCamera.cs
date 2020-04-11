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
		GameEvents.onVesselSOIChanged.Add (onVesselSOIChanged);
		GameEvents.onVesselChange.Add (onVesselChange);
	}

	protected override void Start ()
	{
		cameraPivot = transform.parent;
	}

	protected override void OnDestroy ()
	{
		base.OnDestroy ();
		GameEvents.onVesselSOIChanged.Remove (onVesselSOIChanged);
		GameEvents.onVesselChange.Remove (onVesselChange);
	}

	void onVesselSOIChanged (GameEvents.HostedFromToAction<Vessel, CelestialBody> a)
	{
		if (a.host == FlightGlobals.ActiveVessel) {
			updateReference = true;
		}
	}

	void onVesselChange (Vessel vessel)
	{
		updateReference = true;
	}

	Vector3 primaryReference;
	Vector3 secondaryReference;
	Vector3 primaryVector;
	Vector3 secondaryVector;

	Quaternion deltaRotation;

	bool setRotation;
	bool updateReference;
	bool secondaryVecOK;
	bool secondaryRefOK;
	bool autoRotate;

	const float secondaryEpsilon = 1e-2f;

	const float r = 1;
	const float t = r * r / 2;
	// xyVec is already in -1..1 order of magnitude range (ie, might be a bit
	// over due to screen aspect)
	// This is very similar to blender's trackball calculation (based on it,
	// really)
	public Vector3 TrackballVector (Vector3 xyVec)
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

	public void CalcDragRotationDelta ()
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

	public void CalcPYRotationDelta (Vector2 py)
	{
		Vector3 axis;
		float angle = py.magnitude * 60;
		axis = (cameraPivot.up * py.y - cameraPivot.right * py.x);
		deltaRotation = Quaternion.AngleAxis (angle, axis);
		setRotation = true;
	}

	public void UpdateZoomFov(float delta)
	{
		if (GameSettings.MODIFIER_KEY.GetKey ()) {
			SetFoV (Mathf.Clamp (FieldOfView + delta * 5, fovMin, fovMax));
		} else {
			SetDistance ((1 - delta) * Distance);
		}
	}

	float conv (bool b)
	{
		return b ? 1 : 0;
	}

	void HandleInput ()
	{
		setRotation = false;
		if (Input.GetMouseButton (cameraButton)) {
			CalcDragRotationDelta ();
		}
		float wheel = GameSettings.AXIS_MOUSEWHEEL.GetAxis ();
		if (wheel != 0) {
			UpdateZoomFov (wheel);
		}
		float key = (conv (GameSettings.ZOOM_IN.GetKey ())
					 - conv (GameSettings.ZOOM_OUT.GetKey()));
		if (key != 0) {
			key *= Time.unscaledDeltaTime;
			UpdateZoomFov (key);
		}
		var py = new Vector2 (conv (GameSettings.CAMERA_ORBIT_DOWN.GetKey ())
							  - conv (GameSettings.CAMERA_ORBIT_UP.GetKey ()),
							  conv (GameSettings.CAMERA_ORBIT_LEFT.GetKey ())
							  - conv (GameSettings.CAMERA_ORBIT_RIGHT.GetKey ()));
		py *= Time.unscaledDeltaTime;
		py.x += GameSettings.AXIS_CAMERA_PITCH.GetAxis () * orbitSensitivity;
		py.y += GameSettings.AXIS_CAMERA_HDG.GetAxis () * orbitSensitivity;
		if (py.x != 0 || py.y != 0) {
			CalcPYRotationDelta (py);
		}
	}

	void UpdateCameraAlt ()
	{
		if (vesselTarget != null) {
			cameraAlt = FlightGlobals.getAltitudeAtPos (transform.position, vesselTarget.mainBody);
		} else if (partTarget != null) {
			cameraAlt = FlightGlobals.getAltitudeAtPos (transform.position, partTarget.vessel.mainBody);
		} else {
			cameraAlt = FlightGlobals.getAltitudeAtPos (transform.position);
		}
	}

	Quaternion fromtorot(Vector3 a, Vector3 b)
	{
		float ma = a.magnitude;
		float mb = b.magnitude;
		Vector3 mb_a = mb * a;
		Vector3 ma_b = ma * b;
		float den = 2 * ma * mb;
		float mba_mab = (mb_a + ma_b).magnitude;
		float c = mba_mab / den;
		Vector3 v = Vector3.Cross (a, b) / mba_mab;
		return new Quaternion(v.x, v.y, v.z, c);
	}

	void CalcReferenceVectors ()
	{
		Modes m = mode;
		Vector3 vec;
		Vector3 cbDir;

		if (m == Modes.AUTO) {
			m = autoMode;
		}

		Vessel v = FlightGlobals.ActiveVessel;
		cbDir = v.mainBody.transform.position - v.transform.position;
		switch (m) {
			case Modes.FREE:
				primaryVector = cbDir;
				secondaryVector = (Vector3) v.srf_velocity;
				autoRotate = true;
				break;
			case Modes.CHASE:
				primaryVector = cbDir;
				if (v.targetObject != null) {
					Transform t = v.targetObject.GetTransform ();
					vec = t.position - v.transform.position;
				} else {
					vec = (Vector3) v.srf_velocity;
				}
				secondaryVector = vec;
				autoRotate = true;
				break;
			case Modes.LOCKED:
				autoRotate = false;
				break;
			case Modes.ORBITAL:
				primaryVector = (Vector3) v.obt_velocity;
				secondaryVector = cbDir;
				autoRotate = true;
				break;
		}
		if (autoRotate) {
			vec = Vector3.Dot (secondaryVector, primaryVector) * primaryVector;
			vec /= Vector3.Dot (primaryVector, primaryVector);
			secondaryVector -= vec;
			secondaryVecOK = (secondaryVector.x < -secondaryEpsilon
							  || secondaryVector.x > secondaryEpsilon
							  || secondaryVector.y < -secondaryEpsilon
							  || secondaryVector.y > secondaryEpsilon
							  || secondaryVector.y < -secondaryEpsilon
							  || secondaryVector.z > secondaryEpsilon);
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
		CalcReferenceVectors ();
		if (updateReference) {
			primaryReference = cameraPivot.InverseTransformDirection (primaryVector);
			secondaryReference = cameraPivot.InverseTransformDirection (secondaryVector);
			secondaryRefOK = secondaryVecOK;
			updateReference = false;
		}
		if (secondaryVecOK && !secondaryRefOK) {
			secondaryReference = cameraPivot.InverseTransformDirection (secondaryVector);
		}
		UpdateCameraAlt ();
		if (setRotation) {
			UpdateCameraTransform ();
			cameraPivot.rotation = deltaRotation * pivotRotation;
			primaryReference = cameraPivot.InverseTransformDirection (primaryVector);
			secondaryReference = cameraPivot.InverseTransformDirection (secondaryVector);
			secondaryRefOK = secondaryVecOK;
		} else if (autoRotate) {
			Vector3 priVec = cameraPivot.TransformDirection (primaryReference);
			var rot = fromtorot (priVec, primaryVector);
			if (secondaryRefOK) {
				Vector3 secVec = rot * cameraPivot.TransformDirection (secondaryReference);
				rot = fromtorot (secVec, secondaryVector) * rot;
			}
			UpdateCameraTransform ();
			cameraPivot.rotation = rot * pivotRotation;
		} else {
			UpdateCameraTransform ();
			cameraPivot.rotation = pivotRotation;
		}
	}
}

}
