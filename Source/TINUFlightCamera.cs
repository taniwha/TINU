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
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine;

namespace TINU {

public class TINUFlightCamera : FlightCamera
{
	Transform cameraPivot;
	static int cameraRotateButton = 1;
	static int cameraOffsetButton = 2;

	public static bool disableAll = false;
	public static bool []disableMode = {
			false,	// AUTO
			false,	// FREE
			false,	// ORBITAL
			false,	// CHASE
			false,	// LOCKED
	};
	public static Quaternion []savedRotation = {
		Quaternion.identity,
		Quaternion.identity,
		Quaternion.identity,
		Quaternion.identity,
		Quaternion.identity,
	};
	public static bool invertKeyPitch = false;
	public static bool invertKeyYaw = false;
	public static bool invertCameraOffset = false;
	public static float cameraKeySensitivity = 1;
	public static float sphereScale = 1;

	public static string DataPath { get; private set; }

	bool uiHidden;

	void onHideUI ()
	{
		uiHidden = true;
	}

	void onShowUI ()
	{
		uiHidden = false;
	}

	protected override void Awake ()
	{
		base.Awake ();
		GameEvents.onVesselSOIChanged.Add (onVesselSOIChanged);
		GameEvents.onVesselChange.Add (onVesselChange);
		GameEvents.onHideUI.Add (onHideUI);
		GameEvents.onShowUI.Add (onShowUI);

		DataPath = AssemblyLoader.loadedAssemblies.GetPathByType (typeof (TINUFlightCamera));
		LoadSettings ();
		MapView.OnExitMapView += OnExitMapView;
	}

	static void LoadDisable (ConfigNode n)
	{
		if (!n.HasValue ("disable")) {
			return;
		}
		string []vals = ParseExtensions.ParseArray (n.GetValue ("disable"));
		if (vals.Length > 0) {
			bool.TryParse (vals[0], out disableAll);
		}
		for (int i = 1; i < vals.Length && i <= disableMode.Length; i++) {
			bool.TryParse (vals[i], out disableMode[i - 1]);
		}
	}

	static void LoadRotations (ConfigNode n)
	{
		if (!n.HasNode ("frameRotations")) {
			return;
		}
		ConfigNode rots = n.GetNode ("frameRotations");
		var rotations = rots.GetValues ("rotation");
		for (int i = 0; i < rotations.Length && i < savedRotation.Length; i++) {
			Quaternion q;
			if (ParseExtensions.TryParseQuaternion (rotations[i], out q)) {
				savedRotation[i] = q;
			}
		}
	}

	static void LoadBool (ConfigNode n, string name, ref bool boolVal)
	{
		if (n.HasValue (name)) {
			bool.TryParse (n.GetValue (name), out boolVal);
		}
	}

	static void LoadFloat (ConfigNode n, string name, ref float floatVal)
	{
		if (n.HasValue (name)) {
			float.TryParse (n.GetValue (name), out floatVal);
		}
	}

	public static void LoadSettings ()
	{
		string filePath = DataPath + "/" + "settings.cfg";
		var node = ConfigNode.Load (filePath);
		if (node == null) {
			return;
		}
		foreach (ConfigNode n in node.nodes) {
			if (n.name == "TINU_Settings") {
				LoadDisable (n);
				LoadRotations (n);
				LoadBool (n, "invertCameraOffset", ref invertCameraOffset);
				LoadBool (n, "invertKeyPitch", ref invertKeyPitch);
				LoadBool (n, "invertKeyYaw", ref invertKeyYaw);
				LoadFloat (n, "cameraKeySensitivity", ref cameraKeySensitivity);
				LoadFloat (n, "fovDefault", ref fetch.fovDefault);
				LoadFloat (n, "sphereScale", ref sphereScale);
			}
		}
	}

	static void SaveDisable (ConfigNode node)
	{
		var flags = new bool[6];
		flags[0] = disableAll;
		for (int i = 0; i < disableMode.Length; i++) {
			flags[i + 1] = disableMode[i];
		}
		node.AddValue ("disable", ConfigNode.WriteBoolArray (flags));
	}

	static void SaveRotations (ConfigNode node)
	{
		ConfigNode rots = node.AddNode ("frameRotations");
		for (int i = 0; i < savedRotation.Length; i++) {
			rots.AddValue ("rotation", savedRotation[i]);
		}
	}

	public static void SaveSettings ()
	{
		if (fetch == null) {
			return;
		}
		string filePath = DataPath + "/" + "settings.cfg";
		Directory.CreateDirectory (DataPath);

		var settings = new ConfigNode ("TINU_Settings");
		SaveDisable (settings);
		SaveRotations (settings);
		settings.AddValue ("invertCameraOffset", invertCameraOffset);
		settings.AddValue ("invertKeyPitch", invertKeyPitch);
		settings.AddValue ("invertKeyYaw", invertKeyYaw);
		settings.AddValue ("cameraKeySensitivity", cameraKeySensitivity);
		settings.AddValue ("fovDefault", fetch.fovDefault);
		settings.AddValue ("sphereScale", sphereScale);

		var node = new ConfigNode();
		node.AddNode (settings);
		node.Save (filePath);
	}

	protected override void Start ()
	{
		base.Start ();
		cameraPivot = transform.parent;
	}

	protected override IEnumerator Startup ()
	{
		var baseStartup = base.Startup ();

		while (baseStartup.MoveNext ()) {
			yield return baseStartup.Current;
		}
		flightDistance = Distance;
	}

	protected override void OnDestroy ()
	{
		base.OnDestroy ();
		MapView.OnExitMapView -= OnExitMapView;
		GameEvents.onVesselSOIChanged.Remove (onVesselSOIChanged);
		GameEvents.onVesselChange.Remove (onVesselChange);
		GameEvents.onHideUI.Remove (onHideUI);
		GameEvents.onShowUI.Remove (onShowUI);
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

	TINUCameraState vesselCamera;

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
		float size = Mathf.Min (Screen.height, Screen.width) / 2;
		var center = new Vector3 (Screen.width / 2, Screen.height / 2, 0);
		Vector3 end = (Input.mousePosition - center);
		end /= size * sphereScale;
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

	// keep track of the flight view distance because map view tramples it
	float flightDistance;

	public void UpdateZoomFov(float delta)
	{
		if (GameSettings.MODIFIER_KEY.GetKey ()) {
			SetFoV (Mathf.Clamp (FieldOfView + delta * 5, fovMin, fovMax));
		} else {
			flightDistance = (1 - delta) * Distance;
			SetDistance (flightDistance);
		}
	}

	void OnExitMapView ()
	{
		SetDistance (flightDistance);
	}

	float conv (bool b)
	{
		return b ? 1 : 0;
	}

	void CheckModeKeys ()
	{
		if (Input.GetKeyDown (KeyCode.Keypad4)) {
			SetMode (Modes.LOCKED);
		} else if (Input.GetKeyDown (KeyCode.Keypad5)) {
			SetMode (Modes.CHASE);
		} else if (Input.GetKeyDown (KeyCode.Keypad6)) {
			SetMode (Modes.FREE);
		} else if (Input.GetKeyDown (KeyCode.Keypad8)) {
			SetMode (Modes.ORBITAL);
		} else if (Input.GetKeyDown (KeyCode.Keypad2)) {
			SetMode (Modes.AUTO);
		}
	}

	void CheckFlipKeys ()
	{
		bool reverse = Input.GetKey (KeyCode.RightControl);
		Quaternion frame;
		Quaternion target = transform.rotation;
		Vector3 axis;
		bool switchView = false;

		if (mode == Modes.LOCKED) {
			frame = FlightGlobals.ActiveVessel.ReferenceTransform.rotation;
		} else {
			frame = Quaternion.LookRotation (primaryVector, secondaryVector);
		}

		if (Input.GetKeyDown (KeyCode.Keypad7)) {
			switchView = true;
			if (reverse) {
				target = frame * new Quaternion (1, 0, 0, 0);
			} else {
				target = frame;
			}
		} else if (Input.GetKeyDown (KeyCode.Keypad0)) {
			if (reverse) {
				// right-contol is held, so save the current frame-relative
				// orientation
				savedRotation[(int)mode] = Quaternion.Inverse (frame) * transform.rotation;
			} else {
				switchView = true;
				target = frame * savedRotation[(int)mode];
			}
		} else if (Input.GetKeyDown (KeyCode.Keypad1)) {
			switchView = true;
			if (reverse) {
				target = frame * new Quaternion (0, rootHalf, -rootHalf, 0);
			} else {
				target = frame * new Quaternion (-rootHalf, 0, 0, rootHalf);
			}
		} else if (Input.GetKeyDown (KeyCode.Keypad3)) {
			switchView = true;
			if (reverse) {
				target = frame * new Quaternion (-0.5f, 0.5f, -0.5f, 0.5f);
			} else {
				target = frame * new Quaternion (-0.5f, -0.5f, 0.5f, 0.5f);
			}
		} else if (Input.GetKeyDown (KeyCode.Keypad9)) {
			switchView = true;
			if (reverse) {
				axis = transform.up;
			} else {
				axis = transform.right;
			}
			target = new Quaternion (axis.x, axis.y, axis.z, 0) * target;
		}
		if (switchView) {
			if (mode == Modes.LOCKED) {
				Quaternion q = Quaternion.Inverse (transform.rotation);
				Quaternion t = target;
				deltaRotation = t * q;
				setRotation = true;
			} else {
				Quaternion q = transform.rotation;
				Quaternion t = Quaternion.Inverse (target);
				primaryReference = t * q * primaryReference;
				secondaryReference = t * q * secondaryReference;
			}
		}
	}

	void CheckControlKeys ()
	{
		if (Input.GetKeyDown (KeyCode.KeypadDivide)) {
			if (Input.GetKey (KeyCode.RightControl)) {
				TINU_ConfigWindow.HideGUI ();
				if (disableAll) {
					disableAll = false;
					disableMode[(int)mode] = false;
				} else {
					disableMode[(int)mode] = !disableMode[(int)mode];
				}
			} else {
				TINU_ConfigWindow.ToggleGUI ();
			}
		}
	}

	void HandleInput ()
	{
		setRotation = false;
		if (Input.GetMouseButton (cameraRotateButton)) {
			CalcDragRotationDelta ();
		}
		float wheel = GameSettings.AXIS_MOUSEWHEEL.GetAxis ();
		var eventSystem = UnityEngine.EventSystems.EventSystem.current;
		if (wheel != 0 && (uiHidden || !eventSystem.IsPointerOverGameObject ())) {
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
		if (invertKeyPitch) {
			py.x = -py.x;
		}
		if (invertKeyYaw) {
			py.y = -py.y;
		}
		py *= Time.unscaledDeltaTime;
		py.x += GameSettings.AXIS_CAMERA_PITCH.GetAxis () * orbitSensitivity;
		py.y += GameSettings.AXIS_CAMERA_HDG.GetAxis () * orbitSensitivity;
		py *= cameraKeySensitivity;
		if (py.x != 0 || py.y != 0) {
			CalcPYRotationDelta (py);
		}

		if (Input.GetMouseButton (cameraOffsetButton)) {
			float scale = orbitSensitivity * 0.5f;
			if (invertCameraOffset) {
				scale = -scale;
			}
			offsetHdg -= Input.GetAxis ("Mouse X") * scale;
			offsetPitch += Input.GetAxis ("Mouse Y") * scale;
		}
		if (Mouse.Middle.GetDoubleClick ()) {
			offsetHdg = 0;
			offsetPitch = 0;
			SetDefaultFoV ();
		}
		float offsetClamp = mainCamera.fieldOfView * Mathf.Deg2Rad * 0.6f;
		offsetHdg = Mathf.Clamp (offsetHdg, -offsetClamp, offsetClamp);
		offsetPitch = Mathf.Clamp (offsetPitch, -offsetClamp, offsetClamp);

		CheckModeKeys ();
		CheckFlipKeys ();
		CheckControlKeys ();
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
		if (disableAll || disableMode[(int)mode]) {
			if (HighLogic.LoadedSceneIsFlight) {
				CheckModeKeys ();
				CheckControlKeys ();
			}
			base.LateUpdate ();
			return;
		}
		if (!HighLogic.LoadedSceneIsFlight) {
			return;
		}
		UpdateCameraAlt ();
		if (!updateActive) {
			return;
		}
		Quaternion pivotRotation = cameraPivot.rotation;
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
		if (autoRotate) {
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
		if (InputLockManager.IsUnlocked(ControlTypes.CAMERACONTROLS)
			|| (FlightDriver.Pause
				&& !KSP.UI.UIMasterController.Instance.IsUIShowing)) {
			HandleInput ();
		}
		if (setRotation) {
			pivotRotation = cameraPivot.rotation;
			cameraPivot.rotation = deltaRotation * pivotRotation;
			primaryReference = cameraPivot.InverseTransformDirection (primaryVector);
			secondaryReference = cameraPivot.InverseTransformDirection (secondaryVector);
			UpdateCameraTransform ();
		}
		UpdateEVAFrame ();
		UpdateEuler ();
		if (vesselCamera != null) {
			if (cameraPivot.parent != vesselCamera.Vessel.transform) {
				var vesselTransform = vesselCamera.Vessel.transform;
				var v = Quaternion.Inverse (vesselTransform.rotation);
				vesselCamera.Rotation = v * cameraPivot.rotation;
			} else {
				vesselCamera.Rotation = cameraPivot.localRotation;
			}
			vesselCamera.Distance = Distance;
		}
	}

	protected override void UpdateCameraTransform ()
	{
		if (!(disableAll || disableMode[(int)mode])) {
			camHdg = 0;
			camPitch = 0;
			FoRlerp = 1;
			updateFoR (cameraPivot.rotation, FoRlerp);
		}
		base.UpdateCameraTransform ();
	}

	void FindVesselCameraState (Vessel vessel)
	{
		vesselCamera = null;
		for (int i = 0; i < vessel.vesselModules.Count; i++) {
			if (vessel.vesselModules[i] is TINUCameraState) {
				vesselCamera = vessel.vesselModules[i] as TINUCameraState;
				break;
			}
		}
	}

	public override void SetTargetVessel (Vessel vessel)
	{
		if (vessel != null) {
			FindVesselCameraState (vessel);
		}
		base.SetTargetVessel (vessel);
		if (vesselCamera != null) {
			cameraPivot.localRotation = vesselCamera.Rotation;
			SetDistanceImmediate (vesselCamera.Distance);
		}
	}

	public override void SetTargetPart (Part part)
	{
		if (part != null && part.vessel != null) {
			FindVesselCameraState (part.vessel);
		}
		base.SetTargetPart (part);
		if (vesselCamera != null) {
			cameraPivot.localRotation = vesselCamera.Rotation;
		}
	}
}

}
