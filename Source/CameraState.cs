using UnityEngine;

namespace TINU {

	public class TINUCameraState : VesselModule
	{
		// Default to a 30 degree tilt looking up the vessel's z axis
		public Quaternion Rotation = new Quaternion (0.258819045f, 0, 0, 0.965925826f);

		protected override void OnLoad (ConfigNode node)
		{
			if (node.HasValue ("Rotation")) {
				string quatStr = node.GetValue ("Rotation");
				Rotation = ConfigNode.ParseQuaternion (quatStr);
			}
		}

		protected override void OnSave (ConfigNode node)
		{
			node.AddValue ("Rotation", Rotation);
		}
	}
}
