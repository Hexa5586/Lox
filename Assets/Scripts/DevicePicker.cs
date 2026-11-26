using UnityEngine;

public static class DevicePicker
{
    public static DeviceSelectable Pick(Vector3 screenPosition, Camera camera)
    {
        if (camera == null)
        {
            return null;
        }

        Ray ray = camera.ScreenPointToRay(screenPosition);

        // The nearest collider decides: devices are selectable, anything else occludes.
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            return hit.collider.GetComponentInParent<DeviceSelectable>();
        }

        return null;
    }
}
