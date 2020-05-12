/*
This file is part of Advanced Input.

Advanced Input is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

Advanced Input is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with Advanced Input.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

using KSP.IO;

namespace TINU {

	[KSPAddon (KSPAddon.Startup.Flight, false)]
	public class TINU_ConfigWindow : MonoBehaviour
	{
		int mouseButtons;

//		static GUILayoutOption width400 = GUILayout.Width (400);
//		static GUILayoutOption expandWidth = GUILayout.ExpandWidth (true);
		//static GUILayoutOption noExpandWidth = GUILayout.ExpandWidth (false);

//		const string devShortNameFieldID = "devShortName.TINU";
//		static TextField devShortName = new TextField (devShortNameFieldID);
//		static GUIStyle devNameStyle;

//		static GUILayoutOption numericWidth = GUILayout.Width (127);


		static GUILayoutOption toggleWidth = GUILayout.Width (100);

		static GUIStyle sliderStyle;
		static GUIStyle sliderThumb;

//		static ScrollView devScroll = new ScrollView (150, 300);
//		static ScrollView inputScroll = new ScrollView (150, 250);

#region Basic Window Controls
		static TINU_ConfigWindow instance;
		static bool hide_ui = false;
		static bool gui_enabled = false;
		static Rect windowpos = new Rect (-1, -1, 0, 0);
		static Rect windowdrag = new Rect (0, 0, 10000, 20);

		void onHideUI ()
		{
			hide_ui = true;
			UpdateGUIState ();
		}

		void onShowUI ()
		{
			hide_ui = false;
			UpdateGUIState ();
		}

		void Awake ()
		{
			instance = this;
			TINU_AppButton.Toggle += ToggleGUI;
			GameEvents.onHideUI.Add (onHideUI);
			GameEvents.onShowUI.Add (onShowUI);
		}

		void Start ()
		{
			UpdateGUIState ();
		}

		void OnDestroy ()
		{
			TINU_AppButton.Toggle -= ToggleGUI;
			GameEvents.onHideUI.Remove (onHideUI);
			GameEvents.onShowUI.Remove (onShowUI);
			TINUFlightCamera.SaveSettings ();
		}

		public static void ToggleGUI ()
		{
			gui_enabled = !gui_enabled;
			if (instance != null) {
				instance.UpdateGUIState ();
			}
		}

		public static void HideGUI ()
		{
			gui_enabled = false;
			if (instance != null) {
				instance.UpdateGUIState ();
			}
		}

		public static void ShowGUI ()
		{
			gui_enabled = true;
			if (instance != null) {
				instance.UpdateGUIState ();
			}
		}

		void UpdateGUIState ()
		{
			enabled = !hide_ui && gui_enabled;
			if (!enabled) {
				TINUFlightCamera.SaveSettings ();
			}
		}

		void InitStyles ()
		{
			sliderStyle = new GUIStyle (GUI.skin.horizontalSlider);
			sliderThumb = new GUIStyle (GUI.skin.horizontalSliderThumb);
		}

		void OnGUI ()
		{
			GUI.skin = HighLogic.Skin;
			if (windowpos.x == -1) {
				windowpos = new Rect (Screen.width / 2 - 250,
					Screen.height / 2 - 30, 0, 0);
				InitStyles ();
			}
			windowpos = GUILayout.Window (GetInstanceID (),
				windowpos, WindowGUI,
				"Configuration",
				GUILayout.Width (500));
		}
#endregion

		void VersionInfo ()
		{
			string ver = TINUVersionReport.GetVersion ();
			GUILayout.Label (ver);
		}

		void LoadSettings ()
		{
			if (GUILayout.Button ("Load Settings")) {
				TINUFlightCamera.LoadSettings ();
			}
		}

		void ToggleEnum<T> (ref T curState, T state, string label)
		{
			bool on = curState.Equals (state);
			if (GUILayout.Toggle (on, label, toggleWidth)) {
				curState = state;
			}
		}

		void ToggleBoolNot (ref bool state, string label)
		{
			state = !GUILayout.Toggle (!state, label, toggleWidth);
		}

		void ToggleBool (ref bool state, string label)
		{
			state = GUILayout.Toggle (state, label, toggleWidth);
		}

		void DisableOptions ()
		{
			GUILayout.BeginVertical ();
			ToggleBoolNot (ref TINUFlightCamera.disableAll, "TINU");
			GUILayout.BeginHorizontal ();
			GUILayout.Space(20);
			GUILayout.BeginVertical ();
			for (int i = 0; i < 5; i++) {
				ToggleBoolNot (ref TINUFlightCamera.disableMode[i],
							   ((FlightCamera.Modes) i).displayDescription ());
			}
			GUILayout.EndVertical ();
			GUILayout.EndHorizontal ();
			GUILayout.EndVertical ();
		}

		void InvertCamOffset ()
		{
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Camera Offset");
			GUILayout.FlexibleSpace ();
			ToggleBool (ref TINUFlightCamera.invertCameraOffset, "Invert");
			GUILayout.EndHorizontal ();
		}

		void InvertCamKey ()
		{
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Camera Pitch");
			GUILayout.FlexibleSpace ();
			ToggleBool (ref TINUFlightCamera.invertKeyPitch, "Invert");
			GUILayout.EndHorizontal ();

			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Camera Yaw");
			GUILayout.FlexibleSpace ();
			ToggleBool (ref TINUFlightCamera.invertKeyYaw, "Invert");
			GUILayout.EndHorizontal ();
		}

		void DefaultFoV ()
		{
			float fovMin = FlightCamera.fetch.fovMin;
			float fovMax = FlightCamera.fetch.fovMax;
			float fovDefault = FlightCamera.fetch.fovDefault;

			GUILayout.Label ("Default FoV");
			GUILayout.BeginHorizontal ();
			fovDefault = GUILayout.HorizontalSlider (fovDefault, fovMin, fovMax,
													 sliderStyle, sliderThumb);
			FlightCamera.fetch.fovDefault = Mathf.Floor (fovDefault);
			GUILayout.Label (fovDefault.ToString ("N0"));
			GUILayout.EndHorizontal ();
		}

		void KeySensitivity ()
		{
			float senseMin = 0.2f;
			float senseMax = 2;
			float sense = TINUFlightCamera.cameraKeySensitivity;

			GUILayout.Label ("Sensitivity");
			GUILayout.BeginHorizontal ();
			sense = GUILayout.HorizontalSlider (sense, senseMin, senseMax,
											    sliderStyle, sliderThumb);
			sense = Mathf.Floor (sense * 10) / 10;
			TINUFlightCamera.cameraKeySensitivity = sense;
			GUILayout.Label (sense.ToString ("N1"));
			GUILayout.EndHorizontal ();
		}

		void SphereScale ()
		{
			float sphereMin = 0.75f;
			float sphereMax = 1.25f;
			float sphere = TINUFlightCamera.sphereScale;

			GUILayout.Label ("Sphere Size");
			GUILayout.BeginHorizontal ();
			sphere = GUILayout.HorizontalSlider (sphere, sphereMin, sphereMax,
											    sliderStyle, sliderThumb);
			sphere = Mathf.Floor (sphere * 20) / 20;
			TINUFlightCamera.sphereScale = sphere;
			GUILayout.Label (sphere.ToString ("N2"));
			GUILayout.EndHorizontal ();
		}

		void WindowGUI (int windowID)
		{
			var e = Event.current;
			switch (e.type) {
				case EventType.MouseDown:
					mouseButtons |= 1 << e.button;
					break;
				case EventType.MouseUp:
					mouseButtons &= ~(1 << e.button);
					break;
				case EventType.Layout:
					break;
			}

			GUILayout.BeginVertical ();

			GUILayout.BeginHorizontal ();

			DisableOptions ();
			GUILayout.BeginVertical ();
			InvertCamOffset ();
			InvertCamKey ();
			DefaultFoV ();
			KeySensitivity ();
			SphereScale ();
			GUILayout.EndVertical ();

			GUILayout.EndHorizontal ();

			GUILayout.BeginHorizontal ();
			VersionInfo ();
			LoadSettings ();
			GUILayout.EndHorizontal ();
			GUILayout.EndVertical ();

			GUI.DragWindow (windowdrag);
		}
	}
}
