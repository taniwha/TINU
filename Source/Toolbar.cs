/*
This file is part of TINU.

TINU is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

TINU is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with TINU.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;

namespace TINU {
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class TINU_AppButton : MonoBehaviour
	{
		const ApplicationLauncher.AppScenes buttonScenes = ApplicationLauncher.AppScenes.FLIGHT;
		private static ApplicationLauncherButton button = null;

		public static Callback Toggle = delegate {};

		static bool buttonVisible
		{
			get {
				return true;
			}
		}

		public static void UpdateVisibility ()
		{
			if (button != null) {
				button.VisibleInScenes = buttonVisible ? buttonScenes : 0;
			}
		}

		private void onToggle ()
		{
			Debug.LogFormat("vis: {0}", button.VisibleInScenes);
			Toggle();
		}

		public void Start()
		{
			GameObject.DontDestroyOnLoad(this);
			GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
		}

		void OnDestroy()
		{
			GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
		}

		void OnGUIAppLauncherReady ()
		{
			if (ApplicationLauncher.Ready && button == null) {
				var tex = GameDatabase.Instance.GetTexture("TINU/Textures/icon_button", false);
				button = ApplicationLauncher.Instance.AddModApplication(onToggle, onToggle, null, null, null, null, 0, tex);
				UpdateVisibility ();
			}
		}
	}
}
