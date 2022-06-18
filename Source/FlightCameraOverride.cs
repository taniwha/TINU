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

[KSPAddon (KSPAddon.Startup.Instantly, true)]
public class FlightCameraOverride : MonoBehaviour
{
	const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	void Awake ()
	{
		DontDestroyOnLoad (this);
		GameEvents.onLevelWasLoadedGUIReady.Add (onLevelWasLoadedGUIReady);
	}

	void OnDestroy ()
	{
		GameEvents.onLevelWasLoadedGUIReady.Remove (onLevelWasLoadedGUIReady);
	}

	static FieldInfo audioListener_field;
	static GameObject audioListener;

	internal static void SetAudioListener (TINUFlightCamera tinuCamera)
	{
		if (audioListener_field != null) {
			audioListener_field.SetValue (tinuCamera, audioListener);
		}
	}

	void onLevelWasLoadedGUIReady (GameScenes scene)
	{
		if (scene != GameScenes.PSYSTEM) {
			return;
		}
		Debug.Log ($"[TINU] onLevelWasLoadedGUIReady: {scene} fc: {FlightCamera.fetch}");
		if (FlightCamera.fetch is TINUFlightCamera) {
			return;
		}
		var fcType = typeof (FlightCamera);
		var fcFields = fcType.GetFields (bindingFlags);

		FlightCamera stockCamera = FlightCamera.fetch;
		GameObject scGameObject = stockCamera.gameObject;
		foreach (var field in fcFields) {
			if (field.Name == "AudioListenerGameObject") {
				audioListener_field = field;
				audioListener = (GameObject) field.GetValue (stockCamera);
			}
		}
		FlightCamera.fetch = null;
		var tinuCamera = scGameObject.AddComponent<TINUFlightCamera>();

		foreach (var field in fcFields) {
			field.SetValue (tinuCamera, field.GetValue (stockCamera));
		}
		Destroy (stockCamera);
	}
}

}
