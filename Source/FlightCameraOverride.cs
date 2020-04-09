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
		FlightCamera.fetch = null;
		var tinuCamera = scGameObject.AddComponent<TINUFlightCamera>();

		foreach (var field in fcFields) {
			field.SetValue (tinuCamera, field.GetValue (stockCamera));
		}
		Destroy (stockCamera);
	}
}

}
