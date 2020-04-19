/*
This file is part of TINU.

TINU is free software: you can redistribute it and/or
modify it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

TINU is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with TINU.  If not, see
<http://www.gnu.org/licenses/>.
*/
using System;
using System.Reflection;
using UnityEngine;

namespace TINU {

public class TINUFlightCamera : FlightCamera
{
	Transform cameraPivot;
	static int cameraRotateButton = 1;
	static int cameraOffsetButton = 2;

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

	enum SecondaryAxis {
		None,		// no axis set
		X, Y, Z,	// world reference frame (backup)
		Velocity,
		InVector,
	}

	Vector3 primaryReference;
	Vector3 secondaryReference;
	Vector3 primaryVector;
	Vector3 secondaryVector;
	SecondaryAxis secondaryAxis;

	Quaternion deltaRotation;
	Quaternion evaFoR;

	bool setRotation;
	bool updateReference;
	bool resetSecondaryReference;
	bool autoRotate;

	// this is the square of the velocity, so actually only 3m/s. It may seem
	// high, but it's faster than kerbal running speed, so it keeps the free
	// and chase camera modes usable for a running kerbal.
	const float secondaryEpsilon = 9;

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
		if (Input.GetMouseButton (cameraRotateButton)) {
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

		if (Input.GetMouseButton (cameraOffsetButton)) {
			offsetHdg -= Input.GetAxis ("Mouse X") * orbitSensitivity * 0.5f;
			offsetPitch += Input.GetAxis ("Mouse Y") * orbitSensitivity * 0.5f;
		}
		if (Mouse.Middle.GetDoubleClick ()) {
			offsetHdg = 0;
			offsetPitch = 0;
			SetDefaultFoV ();
		}
		float offsetClamp = mainCamera.fieldOfView * Mathf.Deg2Rad * 0.6f;
		offsetHdg = Mathf.Clamp (offsetHdg, -offsetClamp, offsetClamp);
		offsetPitch = Mathf.Clamp (offsetPitch, -offsetClamp, offsetClamp);
	}

	void UpdateCameraAlt ()
	{
		Vector3 pos = transform.position;

		if (vesselTarget != null) {
			CelestialBody body = vesselTarget.mainBody;
			cameraAlt = FlightGlobals.getAltitudeAtPos (pos, body);
		} else if (partTarget != null) {
			CelestialBody body = partTarget.vessel.mainBody;
			cameraAlt = FlightGlobals.getAltitudeAtPos (pos, body);
			if (partTarget.vessel != FlightGlobals.ActiveVessel) {
				Vessel v = FlightGlobals.ActiveVessel;
				if ((pos - v.transform.position).sqrMagnitude > 2000 * 2000) {
					SetTargetVessel (FlightGlobals.ActiveVessel);
				}
			}
		} else {
			cameraAlt = FlightGlobals.getAltitudeAtPos (pos);
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

	bool ProjectVector (Vector3 normal, ref Vector3 vector)
	{
		float mag = Vector3.Dot (normal, normal);
		vector -= Vector3.Dot (vector, normal) * normal / mag;
		return Vector3.Dot (vector, vector) > secondaryEpsilon;
	}

	void CalcReferenceVectors ()
	{
		SecondaryAxis sAxis = SecondaryAxis.None;

		Modes pm = mode;
		Modes sm = mode;
		Vector3 cbDir;
		Vessel v = FlightGlobals.ActiveVessel;
		bool frameLock = false;

		if (pm == Modes.AUTO) {
			pm = autoMode;
			sm = autoMode;
		} else if (pm == Modes.CHASE) {
			pm = GetAutoModeForVessel (v);
		}

		cbDir = v.mainBody.transform.position - v.transform.position;
		switch (pm) {
			case Modes.FREE:
				primaryVector = cbDir;
				autoRotate = true;
				break;
			case Modes.LOCKED:
				autoRotate = false;
				break;
			case Modes.ORBITAL:
				primaryVector = (Vector3) v.obt_velocity;
				autoRotate = true;
				frameLock = true;
				break;
		}
		// locked and orbital don't use a secondary vector. locked follows
		// the vessel's orientation and oribtal uses frame lock (FIXME would
		// be nice for orbital to use star lock, but that takes messing with
		// the rotating reference frame when below certain altitudes)
		if (sm == Modes.FREE || sm == Modes.CHASE) {
			secondaryVector = v.srf_velocity;
			if (sm == Modes.CHASE && v.targetObject != null) {
				Transform t = v.targetObject.GetTransform ();
				secondaryVector = t.position - v.transform.position;
			}
			sAxis = SecondaryAxis.Velocity;
			frameLock = !ProjectVector (primaryVector, ref secondaryVector);
		}
		if (frameLock) {
			float x = Mathf.Abs (Vector3.Dot (primaryVector, Vector3.right));
			float y = Mathf.Abs (Vector3.Dot (primaryVector, Vector3.forward));
			float z = Mathf.Abs (Vector3.Dot (primaryVector, Vector3.up));
			if (x <= y && x <= z) {
				secondaryVector = Vector3.right;
				sAxis = SecondaryAxis.X;
			} else if (y <= x && y <= z) {
				secondaryVector = Vector3.forward;
				sAxis = SecondaryAxis.Y;
			} else {
				secondaryVector = Vector3.up;
				sAxis = SecondaryAxis.Z;
			}
			ProjectVector (primaryVector, ref secondaryVector);
		}
		if (secondaryAxis != sAxis) {
			secondaryAxis = sAxis;
			resetSecondaryReference = true;
		}
	}

	const float rootHalf = 0.707106781f;
	void UpdateEVAFrame ()
	{
		var rot = Quaternion.LookRotation (-primaryVector, Vector3.down);
		var axis = rot * Vector3.right * rootHalf;
		rot = new Quaternion (axis.x, axis.y, axis.z, rootHalf) * rot;
		FoRlerp = 1;
		updateFoR (rot, FoRlerp);
		evaFoR = rot;
	}
	void UpdateEuler ()
	{
		var evaFwd = evaFoR * Vector3.forward;
		var evaUp = evaFoR * Vector3.up;
		var evaRight = evaFoR * Vector3.right;
		float y = Vector3.Dot (transform.forward, evaFwd);
		float x = Vector3.Dot (transform.forward, evaRight);
		float z = Vector3.Dot (transform.forward, evaUp);
		camHdg = Mathf.Atan2 (x, y);
		camPitch = Mathf.Atan2 (z, Mathf.Sqrt (x * x + y * y));
	}

	protected override void LateUpdate ()
	{
		if (!HighLogic.LoadedSceneIsFlight) {
			return;
		}
		UpdateCameraAlt ();
		if (!updateActive) {
			return;
		}
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
			updateReference = false;
		}
		if (resetSecondaryReference) {
			resetSecondaryReference = false;
			secondaryReference = cameraPivot.InverseTransformDirection (secondaryVector);
		}
		if (setRotation) {
			UpdateCameraTransform ();
			cameraPivot.rotation = deltaRotation * pivotRotation;
			primaryReference = cameraPivot.InverseTransformDirection (primaryVector);
			secondaryReference = cameraPivot.InverseTransformDirection (secondaryVector);
		} else if (autoRotate) {
			Vector3 priVec = cameraPivot.TransformDirection (primaryReference);
			var rot = fromtorot (priVec, primaryVector);
			Vector3 secVec = rot * cameraPivot.TransformDirection (secondaryReference);
			rot = fromtorot (secVec, secondaryVector) * rot;
			UpdateCameraTransform ();
			cameraPivot.rotation = rot * pivotRotation;
		} else {
			UpdateCameraTransform ();
			cameraPivot.rotation = pivotRotation;
		}
		UpdateEVAFrame ();
		UpdateEuler ();
	}

	protected override void UpdateCameraTransform ()
	{
		camHdg = 0;
		camPitch = 0;
		FoRlerp = 1;
		updateFoR (cameraPivot.rotation, FoRlerp);
		base.UpdateCameraTransform ();
	}
}

}
